using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace skininjector_v2
{
    public static class Utils {
        public static void CopyDirectory(string sourceDir, string destinationDir, IEnumerable<string>? excludeFileNames = null)
        {
            excludeFileNames ??= Enumerable.Empty<string>();

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);

                if (excludeFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(destinationDir, fileName), true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                CopyDirectory(subDir, Path.Combine(destinationDir, dirName), excludeFileNames);
            }
        }

    }

}
