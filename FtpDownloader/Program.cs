using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FtpDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Started");

            string localPath;
            if (args.Length>0)
            {
                localPath = args[0];
            }
            else
            {
                localPath = "downloaded_03_11_2019";
                Console.WriteLine($"No argument was specified for local saving path, defaulting to {localPath}");
            }

            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }

            //ftp://10.60.8.217:1024/
            var ftp =new Ftp("ftp://10.60.8.217:1024/");
            var listOfFileLocally = GetListOfFiles(localPath);
            var listOfFilesOnFtp = ftp.GetListOfFiles();

            ftp.DownloadFilesIfTheyDoNotExistLocallyOrAreWrongSize(localPath, listOfFileLocally, listOfFilesOnFtp);

            ftp.DeleteFilesIfExistsLocally(listOfFilesOnFtp, listOfFileLocally, localPath);
        }

        private static Dictionary<string, long> GetListOfFiles(string path)
        {
            var result = new Dictionary<string, long>();
            var files = Directory.GetFiles(path);
            foreach (var file in files)
            {
                var length = new FileInfo(file).Length;
                result[file] = length;
            }

            return result;
        }
    }
}
