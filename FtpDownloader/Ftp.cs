using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace FtpDownloader
{
    public class Ftp
    {
        public string Host { get; set; }

        public Ftp(string host)
        {
            Host = host;
            if (!Host.EndsWith("/"))
            {
                Host += "/";
            }
        }

        public IEnumerable<string> GetListOfFiles()
        {
            var response = ListDirResponse();
            string[] lines = response.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            foreach (var line in lines)
            {
                if (line.StartsWith("total", true, CultureInfo.InvariantCulture))
                {
                    continue;
                }

                if (line.EndsWith("cache", true, CultureInfo.InvariantCulture))
                {
                    continue;
                }

                var lineSplit = line.Split("   ");
                //4843675 Jun 24 20:13 IMG_20190624_201308.jpg
                if (lineSplit.Length < 4)
                {
                    continue;
                }

                var fileName = lineSplit[3].Split(" ").Last();
                yield return fileName;
            }
        }

        private string ListDirResponse()
        {
            var request = (FtpWebRequest)WebRequest.Create(Host);
            request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
            
            using (var response = (FtpWebResponse) request.GetResponse())
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(responseStream))
                    {
                        var result = reader.ReadToEnd();
                        Console.WriteLine();
                        Console.WriteLine($"Directory List Complete, status {response.StatusDescription}");
                        return result;
                    }
                }   
            }
        }

        public async Task DownloadFile(string fileName, string localPath)
        {
            if (!Directory.Exists(localPath))
            {
                Directory.CreateDirectory(localPath);
            }

            var request = (FtpWebRequest) WebRequest.Create($"{Host}{fileName}");
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            var response = await request.GetResponseAsync();
            using (var ftpWebResponse = (FtpWebResponse) response)
            {
                using (var responseStream = ftpWebResponse.GetResponseStream())
                {
                    using (var writeStream = new FileStream(Path.Combine(localPath, fileName), FileMode.Create, FileAccess.ReadWrite,
                        FileShare.ReadWrite, bufferSize: 4096, useAsync: true))
                    {
                        var length = 4096; // size of file 4MB only
                        var buffer = new Byte[length];
                        var bytesRead = await responseStream.ReadAsync(buffer, 0, length);
                        
                        while (bytesRead > 0)
                        {
                            await writeStream.WriteAsync(buffer, 0, bytesRead);
                            bytesRead = await responseStream.ReadAsync(buffer, 0, length); // Read file using await keyword 
                        }
                    }
                }
            }
        }

        public void DownloadFiles(List<string> listOfFiles, string localPath, int simultaneously)
        {
            var allTaskDictionary = new Dictionary<int, string>();
            var taskDictionary = new Dictionary<int, Task>();
            foreach (var file in listOfFiles)
            {
                var task = DownloadFile(file, localPath);
                task.ContinueWith(t =>
                {
                    Console.WriteLine($"Downloading finished {allTaskDictionary[task.Id]}");
                    taskDictionary.Remove(t.Id);
                });
                taskDictionary[task.Id] = task;
                allTaskDictionary[task.Id] = file;
                Console.WriteLine($"Downloading started {allTaskDictionary[task.Id]}");
                
                if (taskDictionary.Count >= simultaneously)
                {
                    Task.WaitAll(taskDictionary.Values.ToArray());
                }
            }
        }
    }
}