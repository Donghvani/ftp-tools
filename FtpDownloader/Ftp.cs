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

        /// <summary>
        /// Get dictionary of values, where key is file name and value is (file size, file size is valid)
        /// </summary>
        public Dictionary<string, (long, bool)> GetListOfFiles()
        {
            var result = new Dictionary<string, (long, bool)>();

            var response = ListDirResponse();
            string[] lines = response.Split(
                new[] {"\r\n", "\r", "\n"},
                StringSplitOptions.None
            );

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("total", true, CultureInfo.InvariantCulture))
                {
                    continue;
                }

                if (line.EndsWith("cache", true, CultureInfo.InvariantCulture))
                {
                    continue;
                }

                var lineSplit = line.Split(" ");
                var fileInfoSplit = lineSplit.Where(ln => !string.IsNullOrWhiteSpace(ln)).ToList();
                var fileSizeString = fileInfoSplit[4];
                var valid = long.TryParse(fileSizeString, out var fileSize);
                var fileName = fileInfoSplit.Last();

                result[fileName] = (fileSize, valid);
            }

            return result;
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
                        Console.WriteLine($"Directory List Complete, status {response.StatusDescription}");
                        return result;
                    }
                }   
            }
        }

        /// <summary>
        /// Download file from ftp
        /// </summary>
        /// <param name="fileName"> file to download</param>
        /// <param name="localPath">file save directory</param>
        /// <returns>Task</returns>
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

        /// <summary>
        /// Download files from ftp
        /// </summary>
        /// <param name="listOfFiles">List of file names</param>
        /// <param name="localPath">File save directory</param>
        /// <param name="simultaneously">How many files download simultaneously</param>
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

            if (taskDictionary.Count > 0)
            {
                Task.WaitAll(taskDictionary.Values.ToArray());
            }
        }

        /// <summary>
        /// Delete file from ftp
        /// </summary>
        /// <param name="fileName">File to delete</param>
        public void DeleteFile(string fileName)
        {
            Console.WriteLine($"Deleting file {fileName}");

            var request = (FtpWebRequest)WebRequest.Create($"{Host}{fileName}");
            request.Method = WebRequestMethods.Ftp.DeleteFile;

            using (var response = (FtpWebResponse) request.GetResponse())
            {
                Console.WriteLine("Delete status: {0}", response.StatusDescription);
            }
        }
        
        /// <summary>
        /// Delete files from ftp if they exist on the local disk
        /// </summary>
        /// <param name="listOfFilesOnFtp">List of files to delete from ftp</param>
        /// <param name="listOfFilesLocally">List of files locally</param>
        public void DeleteFilesIfExistsLocally(Dictionary<string, (long, bool)> listOfFilesOnFtp, Dictionary<string, long> listOfFilesLocally, string localPath)
        {
            var listOfFilesLocallyWithoutLocalPath = GetDictionary(listOfFilesLocally, $"{localPath}/", "");
            foreach (var fileOnFtp in listOfFilesOnFtp)
            {
                if (listOfFilesLocallyWithoutLocalPath.ContainsKey(fileOnFtp.Key))
                {
                    DeleteFile(fileOnFtp.Key);   
                }
            }
        }
        
        /// <summary>
        /// Download Files from ftp if they do not exist on local disk or are having wrong size (are corrupt)
        /// </summary>
        /// <param name="localPath">File save directory</param>
        /// <param name="listOfFileLocally">List of files locally</param>
        /// <param name="listOfFilesOnFtp">Files to download from ftp</param>
        public void DownloadFilesIfTheyDoNotExistLocallyOrAreWrongSize(string localPath, Dictionary<string, long> listOfFileLocally, Dictionary<string, (long, bool)> listOfFilesOnFtp)
        {
            var listOfFilesLocallyWithoutLocalPath = GetDictionary(listOfFileLocally, $"{localPath}/", "");
            var listOfFilesOnFtpToDownload = listOfFilesOnFtp.Where(file => !listOfFilesLocallyWithoutLocalPath.ContainsKey(file.Key)).ToList();
            var filesThatHaveFailedToDownload = CompareFileSizes(listOfFilesLocallyWithoutLocalPath, listOfFilesOnFtp);

            if (listOfFilesOnFtpToDownload.Count + filesThatHaveFailedToDownload.Count == 0)
            {
                Console.WriteLine("Nothing to download");
                return;
            }

            Console.WriteLine($"listOfFilesOnFtpToDownload: {listOfFilesOnFtpToDownload.Count}");
            Console.WriteLine($"filesThatHaveFailedToDownload: {filesThatHaveFailedToDownload.Count}");
            var simultaneously = 4;
            DownloadFiles(listOfFilesOnFtpToDownload.Select(file=>file.Key).ToList(), localPath, simultaneously);
            DownloadFiles(filesThatHaveFailedToDownload.Select(file=>file.Key).ToList(), localPath, simultaneously);
        }
        
        /// <summary>
        /// Get dictionary of files where file names have been changed (replaced path from the file name)
        /// </summary>
        /// <param name="fileDictionary">Dictionary of files</param>
        /// <param name="removePathFromFileName">part of the file name which will get removed from the file name</param>
        /// <param name="replaceWithThis">string that replaces <paramref name="removePathFromFileName"/></param>
        private static Dictionary<string, long> GetDictionary(Dictionary<string, long> fileDictionary, string removePathFromFileName, string replaceWithThis)
        {
            var result = new Dictionary<string, long>();

            foreach (var item in fileDictionary)
            {
                result[item.Key.Replace(removePathFromFileName, replaceWithThis)] = item.Value;
            }

            return result;
        }
        
        /// <summary>
        /// Compare file sizes on the local disk and ftp server
        /// </summary>
        /// <param name="local">Dictionary of files locally</param>
        /// <param name="ftp">Dictionary of files on the ftp server</param>
        /// <returns></returns>
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