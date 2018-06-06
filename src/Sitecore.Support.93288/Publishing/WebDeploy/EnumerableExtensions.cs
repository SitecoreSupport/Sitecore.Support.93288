using System;
using System.Collections.Generic;

namespace Sitecore.Support.Publishing.WebDeploy
{
  internal static class EnumerableExtensions
  {
    public static void Apply<T>(this IEnumerable<T> sequence, Action<T> action)
    {
      foreach (var e in sequence)
      {
        action(e);
      }
    }
  }
}