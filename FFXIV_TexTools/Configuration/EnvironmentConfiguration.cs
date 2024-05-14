using Microsoft.Win32;

namespace FFXIV_TexTools.Configuration
{
    internal class EnvironmentConfiguration
    {
        // Disable DX9 hardware accelerated rendering of WPF UI
        // The newest versions of DXVK no longer have major rendering issues that would require this
        internal static bool TT_Software_Rendering = GetEnvironmentFlag("TT_SOFTWARE_RENDERING", false);

        // Bypass DX11 / DX9 shared rendering, to avoid an issue with DXVK where model previews do not work
        // If this environment variable is not set, it attempts to default to true when using WINE / Linux
        internal static bool TT_Unshared_Rendering = GetEnvironmentFlag("TT_UNSHARED_RENDERING", DetectWINE());

        static bool GetEnvironmentFlag(string varName, bool defaultValue = false)
        {
            try
            {
                string value = System.Environment.GetEnvironmentVariable(varName);
                if (uint.TryParse(value, out var intval))
                    return intval != 0;
                else if (bool.TryParse(value, out var boolval))
                    return boolval;
                else
                    return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        static bool DetectWINE()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey("Software\\Wine");
                if (key != null)
                {
                    key.Dispose();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
