using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using FFXIV_TexTools.Controls;
using System.Windows.Controls;
using FFXIV_TexTools.Views.Metadata;
using System.Windows.Data;
using System.Windows.Markup.Primitives;
using System.Diagnostics;
using System.Globalization;
using System.Resources;
using System.ComponentModel;

namespace FFXIV_TexTools.Resources
{
    /// <summary>
    /// Localization class:
    /// This class implements the localization function
    /// You can write the original text and string directly into the code,
    /// No resource ID needs to be assigned
    /// Localize without adding anything to the resource file
    /// Use in Code:
    ///     "this to Localization".L()//Return localized string
    ///     $"The author of this book is {author._()}".L()//Return localized string
    ///     
    /// Use in XAML window or control elements:
    ///     xmlns:resx="clr-namespace:FFXIV_TexTools.Resources"
    ///     resx:Localization.Enabled="True"
    ///Enable XAML localization
    /// </summary>
    public class Localization: FrameworkElement
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached("Enabled", typeof(bool?), typeof(Localization),new PropertyMetadata(null,OnEnabledChanged));
        public static bool? GetEnabled(DependencyObject obj)
        {
            return (bool?)obj.GetValue(EnabledProperty);
        }
        public static void SetEnabled(DependencyObject obj, bool? value)
        {
            obj.SetValue(EnabledProperty, value);
        }
        /// <summary>
        /// Get localized string
        /// It is not recommended to use it directly. Please use stringlocalizationextension.L()
        /// </summary>
        /// <param name="rawKey">Resource key</param>
        /// <returns>localized string</returns>
        public static string GetString(string rawKey)
        {
            var key = rawKey.Trim();
            UIMessages.ResourceManager.IgnoreCase = true;
            var result=UIMessages.ResourceManager.GetString(key);
            if (result == null)
            {
                UIStrings.ResourceManager.IgnoreCase = true;
                result = UIStrings.ResourceManager.GetString(key);
            }
            if (result == null)
                result = rawKey;
            //var uiStrFile = "d:\\UIStr.txt";
            //var uiStrLines = new List<string>();
            //if (File.Exists(uiStrFile))
            //    uiStrLines.AddRange(File.ReadAllLines(uiStrFile));
            //var line = result;
            //if (Encoding.UTF8.GetBytes(line).Length == line.Length)
            //{
            //    if (!uiStrLines.Contains(line))
            //    {
            //        uiStrLines.Add(line);
            //        File.AppendAllText(uiStrFile, line + "\r\n");
            //    }
            //}
            return result;
        }
        private static DependencyProperty GetDependencyProperty(Type type,string name)
        {
            Type dobjType = type;
            while (true)
            {
                //Task.Yield();
                var dpInfo = dobjType.GetField(name, BindingFlags.Static | BindingFlags.Public);
                if (dpInfo != null)
                {
                    var dpv = dpInfo.GetValue(null);
                    if (dpv != null)
                    {
                        return dpv as DependencyProperty;
                    }
                    break;
                }
                dobjType = dobjType.BaseType;
                if (dobjType == null)
                {
                    break;
                }
            }
            return null;
        }
        private static (DependencyObject Target,DependencyProperty Property,Binding Binding) GetBingingInfo((PropertyInfo PropertyInfo, Object Target) it)
        {
            FieldInfo dpInfo;
            Binding binding = null;
            DependencyProperty property=null;
            var dpobj = it.Target as DependencyObject;
            if (dpobj == null)
                return (dpobj, property, binding);
            Type dobjType = it.Target.GetType();
            property = GetDependencyProperty(it.Target.GetType(), $"{it.PropertyInfo.Name}Property");
            if (property != null)
            {
                binding = BindingOperations.GetBinding(dpobj, property);
            }            
            return (dpobj, property, binding);
        }
        private static void Element_Loaded(object sender, RoutedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var elmList = new Dictionary<object, bool>();
            GetElementList(elmList, element);
            var list = new List<(PropertyInfo PropertyInfo, Object Target)>();
            foreach (var elm in elmList.Keys)
            {
                //GetStringPropertyInfos(elmList, list, elm);
                var info= GetStringPropertyInfo(elm.GetType());
                if (info == null)
                {
                    var stringProperty = elm.GetType().GetProperty("Content");
                    if (stringProperty == null)
                    {
                        continue;
                    }
                    var contentValue = stringProperty.GetValue(elm, null);
                    if (contentValue == null)
                    {
                        continue;
                    }
                    if (contentValue.GetType() == typeof(string))
                    {
                        if (!String.IsNullOrEmpty((contentValue as string).Trim()))
                        {
                            list.Add((stringProperty, elm));
                        }
                        continue;
                    }
                }
                else
                {
                    list.Add((info,elm));
                }
            }

            string line = "";

            foreach (var it in list)
            {
                if (it.Target == null||it.PropertyInfo==null)
                    continue;
                var key = it.PropertyInfo?.GetValue(it.Target)?.ToString()?.Trim();
                if (key == null && it.Target.GetType() == typeof(string))
                    key = it.Target.ToString();
                if (string.IsNullOrEmpty(key))
                    continue;
                var text = GetString(key);

                if (text != null)
                {
                    line = text;

                    var bindingInfo = GetBingingInfo(it);
                    if (bindingInfo.Binding != null)
                    {
                        continue;
                    }

                    var dobj = it.Target as DependencyObject;
                    var dp = GetDependencyProperty(it.Target.GetType(), it.PropertyInfo.Name + "Property");
                    if (dobj != null&&dp!=null)
                    {
                        dobj.SetValue(dp, text);
                    }
                    else
                    {
                        it.PropertyInfo.SetValue(it.Target, text, null);
                    }
                }
            }
            element.Loaded -= Element_Loaded;
        }
        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetEnabled(d, (bool?)e.NewValue);
            var val = (bool?)e.NewValue;
            if (val == null)
                return;
            if (val.Value)
            {
                var element = d as FrameworkElement;
                if (element == null)
                    return;

                element.Loaded += Element_Loaded;
            }
        }
        private static void GetElementList(Dictionary<object, bool>  list,object item)
        {
            if((item as TreeView)!=null)
            {
                return;
            }
            if (item == null)
                return;
            if(!list.ContainsKey(item))
                list.Add(item,(item as UIElement)!=null);
            var dobj = item as DependencyObject;
            if (dobj == null)
                return;
            var its = LogicalTreeHelper.GetChildren(dobj);
            foreach (var it in its)
            {
                GetElementList(list, it);
            }
        }
        private static PropertyInfo GetStringPropertyInfo(Type type)
        {
            string[] names = new[]
            {
                "Text",
                "Title",
                "Header",
                "ButtonText",
                "DescriptionText"
            };
            foreach(var name in names)
            {
                var stringProperty = type.GetProperty(name);
                if (stringProperty != null)
                {
                    return stringProperty;
                }
            }
            return null;
        }
    }
}
namespace FFXIV_TexTools
{
    /// <summary>
    /// String Localization Extension
    /// eg:
    ///     "this to Localization".L()//Return localized string
    ///     $"The author of this book is {author._()}".L()//Return localized string
    /// </summary>
    public static class StringLocalizationExtension
    {
        static readonly Dictionary<MethodBase, List<object>> FormatArgsDic = new Dictionary<MethodBase, List<object>>();
        /// <summary>
        /// Get localized string
        /// eg:
        ///     "this to Localization".L()//Return localized string
        /// </summary>
        /// <param name="rawKey">Resource key</param>
        /// <returns>localized string</returns>
        public static string L(this string rawKey)
        {
            var st = new StackTrace();
            var caller = st.GetFrame(1).GetMethod();

            object[] args=new object[]{ };
            if (FormatArgsDic.ContainsKey(caller))
            {
                args = FormatArgsDic[caller].ToArray();
                FormatArgsDic.Remove(caller);
            }
            var realKey = rawKey;
            if(args.Length > 0)
            {
                for(var i= 0; i < args.Length; i++)
                {
                    var arg=args[i].ToString();
                    var b= realKey.IndexOf(arg);
                    if(b == -1)
                    {
                        return rawKey;
                    }
                    realKey = realKey.Remove(b,arg.Length);
                    realKey = realKey.Insert(b, $"{{{i}}}");
                }
            }
            var result = FFXIV_TexTools.Resources.Localization.GetString(realKey);
            if (result == realKey)
            {
                return rawKey;
            }
            if (args.Length > 0)
            {
                try
                {
                    var tArgs= args.Select(it=> FFXIV_TexTools.Resources.Localization.GetString(it.ToString())).ToArray();
                    result =String.Format(result, tArgs);
                }
                catch
                {
                    result = rawKey;
                }
            }
            //Used by {0}/{1} Variants
            return result;
        }
        /// <summary>
        /// Format string localization placeholder
        /// eg:
        ///     $"The author of this book is {author._()}".L()//Return localized string
        /// </summary>
        /// <param name="obj">Any object</param>
        /// <returns>itself</returns>
        public static object _(this object obj)
        {
            var st = new StackTrace();
            var caller=st.GetFrame(1).GetMethod();
            if (!FormatArgsDic.ContainsKey(caller))
            {
                FormatArgsDic.Add(caller,new List<object>());
            }            
            FormatArgsDic[caller].Add(obj);
            return obj;
        }
    }
}