using System;
using System.Linq;
using System.Threading.Tasks;

namespace FtpDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Started");
            
            var ftp =new Ftp("ftp://192.168.0.3:1024/");
            var listOfFiles = ftp.GetListOfFiles().ToList();

            var simultaneously = 4;
            var localPath = "downloaded";
            ftp.DownloadFiles(listOfFiles, localPath, simultaneously);
        }
    }
}
