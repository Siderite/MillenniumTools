using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace MillenniumTools.Common
{
    public static class ExtensionMethods
    {
        private static Random _rnd = new Random();

        public static object GetLock(this ICollection coll)
        {
            return coll.SyncRoot;
        }

        public static void AddRange<T>(this BindingList<T> list, IEnumerable<T> items, bool clearFirst=false)
        {
            list.RaiseListChangedEvents = false;
            if (clearFirst)
            {
                try
                {
                    list.Clear();
                }
                catch
                {
                    if (list.Count > 0)
                    {
                        list.RemoveAt(0);
                    }
                    list.Clear();
                }
            }
            foreach (var item in items)
            {
                list.Add(item);
            }
            list.RaiseListChangedEvents = true;
        }

        /*public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }*/

        public static void Clear<T>(this T[] array, T value=default(T))
        {
            for (var i = 0; i < array.Length; i++) array[i] = value;
        }

        public static T Random<T>(this IEnumerable<T> source, Func<T, bool> selector)
        {
            var list = source.Where(selector).ToList();
            if (list.Count == 0) return (T)typeof(T).GetDefaultValue();
            var idx = _rnd.Next(0, list.Count);
            return list[idx];
        }

        public static IEnumerable<object> Select(this IEnumerable source, Func<object, object> selector)
        {
            source=source??new object[0];
            return Enumerable.Select((IEnumerable<object>)source,selector);
        }

        public static object GetDefaultValue(this Type t)
        {
            if (t.IsValueType)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                return null;
            }
        }

        public static Brush Lighter(this Brush brush, double perc)
        {
            var sb = brush as SolidColorBrush;
            if (sb != null)
            {
                var color = (HSLColor)sb.Color;
                color.Luminosity *= (1 + perc / 100);
                return new SolidColorBrush(color);
            }
            throw new NotImplementedException(brush.GetType().FullName);
        }

        public static void Fire(this EventHandler ev, object sender=null)
        {
            if (ev == null) return;
            ev(sender, EventArgs.Empty);
        }

        public static void LogInfo(this object obj, string message)
        {
            log4net.ILog _log = log4net.LogManager.GetLogger(obj.GetType());
            _log.Info(message);
        }

        public static double Constrain(this double val, double? min, double? max)
        {
            if (double.IsNaN(val)) return min ?? 0;
            if (double.IsNegativeInfinity(val)) return min ?? 0;
            if (double.IsPositiveInfinity(val)) return max ?? 0;
            if (min != null && val < min.Value) return min.Value;
            if (max != null && val > max.Value) return max.Value;
            return val;
        }
    }
}
