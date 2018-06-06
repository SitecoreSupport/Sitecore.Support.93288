using System;
using System.Collections.Generic;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Publishing;
using Sitecore.Publishing.WebDeploy;
using Sitecore.Publishing.WebDeploy.Decorators;
using Sitecore.Publishing.WebDeploy.Sites;
using Sitecore.StringExtensions;
using Sitecore.Threading;

namespace Sitecore.Support.Publishing.WebDeploy
{
  public class PublishHandler
  {
    List<Task> _tasks = new List<Task>();

    public void OnPublish(object sender, EventArgs args)
    {
      var publisher = SitecoreEventArgs.GetObject(args, 0) as Publisher;
      if (publisher == null)
      {
        return;
      }

      if (this.Synchronous)
      {
        Run(publisher);
      }
      else
      {
        ManagedThreadPool.QueueUserWorkItem(o =>
        {
          lock (this)
          {
            this.Run(publisher);
          }
        });
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this <see cref="PublishHandler"/> is synchronous.
    /// </summary>
    /// <value><c>true</c> if synchronous; otherwise, <c>false</c>.</value>
    public bool Synchronous
    {
      get; set;
    }

    /// <summary>
    /// Adds the task.
    /// </summary>
    /// <param name="task">The task.</param>
    public void AddTask(Task task)
    {
      _tasks.Add(task);
    }

    /// <summary>
    /// Runs with the specified publisher.
    /// </summary>
    /// <param name="publisher">The publisher.</param>
    private void Run(Publisher publisher)
    {
      foreach (var task in this._tasks)
      {
        if (task.TargetDatabase != null && publisher.Options.TargetDatabase.Name != task.TargetDatabase)
        {
          continue;
        }

        try
        {
          DeploymentTaskRunner runner = this.GetRunner(task);
          runner.Execute();
        }
        catch (Exception e)
        {
          Log.Error(e.Message, e, this);
        }
      }
    }

    /// <summary>
    /// Gets the runner for specific task.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <returns>The runner.</returns>
    private DeploymentTaskRunner GetRunner(Task task)
    {
      var runner = new DeploymentTaskRunner();
      if (task.LocalRoot != null)
      {
        runner.SourceSite = new FolderDeploymentSite(task.LocalRoot);
      }
      else
      {
        runner.SourceSite = new LocalApplicationSite();
      }
      runner.TargetSite = new FolderDeploymentSite(task.RemoteRoot);
      if (task.TargetServer != null)
      {
        runner.TargetSite.Decorators.Add(new RemoteDecorator { ComputerName = task.TargetServer, UserName = task.UserName, Password = task.Password });
      }
      runner.SyncOptions.DoNotDelete = false;
      runner.SyncOptions.UseChecksum = true;
      runner.SourceSite.Decorators.Add(new TraceDecorator((level, message, data) => Log.Info("WebDeploy {0} : {1}".FormatWith(level, message), this)));
      runner.TargetSite.Decorators.Add(new TraceDecorator((level, message, data) => Log.Info("WebDeploy {0} : {1}".FormatWith(level, message), this)));

      task.Paths.Apply(path => runner.Paths.Add(path));
      return runner;
    }
  }
}
