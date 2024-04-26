using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for HexTexBox.xaml
    /// </summary>
    public partial class HexTextBox : UserControl
    {
        public HexTextBox()
        {
            InitializeComponent();
        }
    }

    public class HexValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var str = value as string;
            if (String.IsNullOrWhiteSpace(str))
            {
                return new ValidationResult(true, null);
            }
            str = str.ToLower();
            var rx = new Regex("^[0-9a-f]*$");
            if(!rx.Match(str).Success)
            {
                return new ValidationResult(false, "Input is not a valid hex value");
            }
            return new ValidationResult(true, null);
        }
    }

    // Kind of hacky, but whatever.  Binding values for converters is a nightmare.
    public class HalfHexValueConverter : HexValueConverter
    {
        protected override int GetLength()
        {
            return 4;
        }
    }
    public class UnlimitedHexValueConverter : HexValueConverter
    {
        protected override int GetLength()
        {
            return -1;
        }
    }
    public  class HexValueConverter : IValueConverter
    {
        protected virtual int GetLength()
        {
            return 8;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var val = System.Convert.ToInt64(value);
                var hexSt = String.Format("{0:X}", val);
                if (GetLength() > 0)
                {
                    while (hexSt.Length < GetLength())
                    {
                        hexSt = "0" + hexSt;
                    }
                }
                return hexSt;
            }
            catch (Exception)
            {
                return value;
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var val = value.ToString().ToUpper();
                if(string.IsNullOrWhiteSpace(val))
                {
                    return 0;
                }
                var ret = Int64.Parse(val, System.Globalization.NumberStyles.HexNumber);
                return ret;

            }
            catch (Exception)
            {
                return value;
            }
        }
    }

}
