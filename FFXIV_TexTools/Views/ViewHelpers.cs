using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFXIV_TexTools.Views
{
    public static class ViewHelpers
    {
        public static Progress<(int current, int total, string message)> BindReportProgress(ProgressDialogController controller)
        {
            return new Progress<(int current, int total, string message)>(BindReportProgressAction(controller));
        }
        public static Action<(int current, int total, string message)> BindReportProgressAction(ProgressDialogController controller)
        {
            Action<(int current, int total, string message)> f = ((int current, int total, string message) report) =>
            {
                if (!report.message.Equals(string.Empty))
                {
                    controller.SetMessage(report.message.L());
                }
                else
                {
                    controller.SetMessage($"{UIMessages.PleaseStandByMessage}");
                }

                if (report.total > 0)
                {
                    var value = (double)report.current / (double)report.total;
                    controller.SetProgress(value);
                }
                else
                {
                    controller.SetIndeterminate();
                }
            };

            return f;
        }

    }
}
