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
                localPath = "downloaded";
                Console.WriteLine($"No argument was specified for local saving path, defaulting to {localPath}");
            }

            var ftp =new Ftp("ftp://192.168.0.3:1024/");
            var listOfFileLocally = GetListOfFiles(localPath);
            var listOfFilesOnFtp = ftp.GetListOfFiles();

            ftp.DownloadFilesIfTheyDoNotExistLocallyOrAreWrongSize(localPath, listOfFileLocally, listOfFilesOnFtp);
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
