using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FtpDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Started");
            
            var localPath = "downloaded";
            var ftp =new Ftp("ftp://192.168.0.3:1024/");
            var listOfFileLocally = GetListOfFiles(localPath);
            var listOfFilesOnFtp = ftp.GetListOfFiles();

            Download(ftp, localPath, listOfFileLocally, listOfFilesOnFtp);
        }

        private static void Delete(Ftp ftp, Dictionary<string, (long, bool)> listOfFilesOnFtp, Dictionary<string, long> listOfFileLocally)
        {
            foreach (var fileOnFtp in listOfFilesOnFtp)
            {
                if (listOfFileLocally.ContainsKey(fileOnFtp.Key))
                {
                    ftp.DeleteFile(fileOnFtp.Key);   
                }
            }
        }

        private static void Download(Ftp ftp, string localPath, Dictionary<string, long> listOfFileLocally, Dictionary<string, (long, bool)> listOfFilesOnFtp)
        {
            var listOfFilesLocallyWithoutLocalPath = GetDictionary(listOfFileLocally, $"{localPath}/", "");

            var listOfFilesOnFtpToDownload = listOfFilesOnFtp.Where(file => !listOfFilesLocallyWithoutLocalPath.ContainsKey(file.Key)).ToList();
            var filesThatHaveFailedToDownload = CompareFileSizes(listOfFilesLocallyWithoutLocalPath, listOfFilesOnFtp);

            if (listOfFilesOnFtpToDownload.Count + filesThatHaveFailedToDownload.Count == 0)
            {
                Console.WriteLine("Nothing to download");
                return;
            }

            var simultaneously = 4;
            ftp.DownloadFiles(listOfFilesOnFtpToDownload.Select(file=>file.Key).ToList(), localPath, simultaneously);
            ftp.DownloadFiles(filesThatHaveFailedToDownload.Select(file=>file.Key).ToList(), localPath, simultaneously);
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
        
        private static Dictionary<string, long> GetDictionary(Dictionary<string, long> fileDictionary, string removePathFromFileName, string replaceWithThis)
        {
            var result = new Dictionary<string, long>();

            foreach (var item in fileDictionary)
            {
                result[item.Key.Replace(removePathFromFileName, replaceWithThis)] = item.Value;
            }

            return result;
        }

        private static Dictionary<string, (long sizeLocal, long sizeOnFtp)> CompareFileSizes(Dictionary<string, long> local, Dictionary<string, (long, bool)> ftp)
        {
            var result = new Dictionary<string, (long sizeLocal, long sizeOnFtp)>();
            foreach (var localFileItem in local)
            {
                var key = localFileItem.Key;
                if (ftp.ContainsKey(key))
                {
                    var ftpItem = ftp[key];
                    if (ftpItem.Item1 != localFileItem.Value)
                    {
                        result[key] = (localFileItem.Value, ftpItem.Item1);
                    }
                }
            }

            return result;
        }
    }
}
