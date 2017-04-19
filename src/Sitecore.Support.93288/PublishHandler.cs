using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Publishing;
using Sitecore.Publishing.WebDeploy;
using Sitecore.Publishing.WebDeploy.Decorators;
using Sitecore.Publishing.WebDeploy.Sites;
using Sitecore.StringExtensions;
using Sitecore.Threading;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Sitecore.Support.Publishing.WebDeploy
{
    public class PublishHandler
    {
        private List<Task> _tasks = new List<Task>();

        public bool Synchronous
        {
            get;
            set;
        }

        public bool SkipIfNonMediaSingleItemPublish
        {
            get;
            set;
        }

        public bool UseChecksum
        {
            get;
            set;
        }

        public PublishHandler()
        {
            this.SkipIfNonMediaSingleItemPublish = false;
            this.UseChecksum = true;
        }

        public void AddTask(Task task)
        {
            this._tasks.Add(task);
        }

        private DeploymentTaskRunner GetRunner(Task task)
        {
            DeploymentTaskRunner runner = new DeploymentTaskRunner();
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
                RemoteDecorator remoteDecorator = new RemoteDecorator();
                remoteDecorator.ComputerName = task.TargetServer;
                remoteDecorator.UserName=task.UserName;
                remoteDecorator.Password =task.Password;
                RemoteDecorator item = remoteDecorator;
                runner.TargetSite.Decorators.Add(item);
            }
            runner.SyncOptions.DoNotDelete = false;
            runner.SyncOptions.UseChecksum = this.UseChecksum;
            runner.SourceSite.Decorators.Add(new TraceDecorator(delegate (string level, string message, object data)
            {
                Log.Info("WebDeploy {0} : {1}".FormatWith(new object[] { level, message }), this);

            }));
            runner.TargetSite.Decorators.Add(new TraceDecorator(delegate (string level, string message, object data)
            {
                Log.Info("WebDeploy {0} : {1}".FormatWith(new object[] { level, message }), this);

            }));
            task.Paths.Apply<string>(delegate (string path) {
                runner.Paths.Add(path);
            });
            return runner;
        }

        public void OnPublish(object sender, EventArgs args)
        {
            WaitCallback waitCallback = null;
            Publisher publisher = SitecoreEventArgs.GetObject(args, 0) as Publisher;
            if (publisher != null)
            {
                if (this.Synchronous)
                {
                    this.Run(publisher);
                    return;
                }
                if (waitCallback == null)
                {
                    waitCallback = delegate (object o)
                    {
                        lock (this)
                        {
                            this.Run(publisher);
                        }
                    };
                }
                ManagedThreadPool.QueueUserWorkItem(waitCallback);
            }
        }

        private void Run(Publisher publisher)
        {
            foreach (Task current in this._tasks)
            {
                if (current.TargetDatabase != null)
                {
                    if (!(publisher.Options.TargetDatabase.Name == current.TargetDatabase))
                    {
                        continue;
                    }
                }
                try
                {
                    this.GetRunner(current).Execute();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.Message, ex, this);
                }
            }
        }
    }
    public static class EnumerableExtensions
    {
        // Methods
        public static void Apply<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (T local in sequence)
            {
                action(local);
            }
        }
    }



}
