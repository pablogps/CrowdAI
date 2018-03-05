using System;
using System.IO;
using System.Collections.Generic;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.Genomes.Neat;
using SharpCompress.Readers;
using Evolution.UsersInfo;
using System.Linq;

namespace SharpNeat
{
    public static class PopulationReadWrite
    {
        static private bool isRelease = true;
        public static bool IsRelease { get; }

        static private string projectBasePath;

        // TODO: FIX IT. A LOT of repeated code here!! (See: EventsController)
        static public void WriteLineForDebug(string line, string user)
        {
            try
            {
                line += " " + DateTime.Now.ToString();
                string path = ReturnLogPath(user);
                string[] lines = { line };
                File.AppendAllLines(path, lines);
                System.Diagnostics.Debug.WriteLine(line);
            }
            catch (Exception e)
            {
                string[] errorLine = { "Fatal error when writing to log from ActiveUsersLists: " + e.Message + " " + DateTime.Now.ToString() };
                File.AppendAllLines(@"C:\inetpub\wwwroot\Output_FatalErrors.txt", errorLine);
                return;
            }
        }

        static string ReturnLogPath(string user)
        {
            string path = "";
            if (user != null)
            {
                string folderPath = @"C:\inetpub\wwwroot\Logs\" + user;
                CreateFolderIfNotExists(folderPath);
                path = folderPath + "\\Output_Malmo.txt";
            }
            else
            {
                path = @"C:\inetpub\wwwroot\Output_Malmo.txt";
            }
            return path;
        }

        static public void WriteErrorForDebug(string line)
        {
            line += " " + DateTime.Now.ToString();
            string[] errorLine = { line };
            File.AppendAllLines(@"C:\inetpub\wwwroot\Output_FatalErrors.txt", errorLine);
        }

        static PopulationReadWrite()
        {
            projectBasePath = AppDomain.CurrentDomain.BaseDirectory;
            if (projectBasePath == @"C:\Malmo\INM\INM\INM\")
            {
                isRelease = false;
            }
            else
            {
                isRelease = true;
            }
        }

        static string UserDefaultFolder(string userName)
        {
            string userDefaultFolder = GetEvolutionFolderPath();
            userDefaultFolder += "ExperimentsData/" + userName + "/";
            return userDefaultFolder;
        }

        static public string PopulationPathForUser(string userName)
        {
            return UserDefaultFolder(userName) + "_Population.xml";
        }

        static string PopulationDefaultPath(string folderPath)
        {
            return folderPath + "_Population.xml";
        }

        static string ChampionDefaultPath(string folderPath)
        {
            return folderPath + "_Champion.xml";
        }

        static public string GetEvolutionFolderPath()
        {
            string evolutionFolderPath;
            if (!isRelease)
            {
                evolutionFolderPath = Path.GetFullPath(Path.Combine(projectBasePath, @"..\"));
            }
            else
            {
                evolutionFolderPath = projectBasePath;
            }
            // And down to Evolution
            evolutionFolderPath = Path.GetFullPath(Path.Combine(evolutionFolderPath, @".\Evolution\"));
            return evolutionFolderPath;
        }

        static public void SavePopulation(string userName)
        {
            SavePopulationAtPath(UserDefaultFolder(userName), userName);
        }

        static void SavePopulationAtPath(string folderPath, string userName)
        {
            CreateFolderIfNotExists(folderPath);
            System.Diagnostics.Debug.WriteLine("Try SavePopulation with folderPath " + folderPath);
            SavePopulation(folderPath, userName);
            SaveChampion(folderPath, userName);
        }

        static void SavePopulation(string folderPath, string userName)
        {
            // T try block is an extra precaution. Problems always appear in SaveChampion,
            // but the same principle could work here...
            NeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm = null;
            try
            {
                evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
            }
            catch (NullReferenceException ex)
            {
                string errorLine = "User " + userName + " encountered an exception during SavePopulation in PopulationReadWrite: " + ex.Message;
                WriteErrorForDebug(errorLine);
                return;
            }
            if (evolutionAlgorithm != null)
            {
                var doc = NeatGenomeXmlIO.SaveComplete(evolutionAlgorithm.GenomeList, false);
                doc.Save(PopulationDefaultPath(folderPath));
            }
        }

        static void SaveChampion(string folderPath, string userName)
        {
            // We use this seemingly unnecessary method (TryGetChampion) because
            // simultaneous users have triggered exceptions here!
            List<NeatGenome> onlyTheChamp = TryGetChampion(userName);
            if (onlyTheChamp != null)
            {
                var doc = NeatGenomeXmlIO.SaveComplete(onlyTheChamp, false);
                doc.Save(ChampionDefaultPath(folderPath));
            }
        }

        static List<NeatGenome> TryGetChampion(string userName)
        {
            List<NeatGenome> genome = null;
            try
            {
                // The exception comes from old users trying to access their evolution algorithm when they 
                // have been removed from the ActiveUsersList!
                var evolutionAlgorithm = ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName);
                genome = new List<NeatGenome>() { evolutionAlgorithm.CurrentChampGenome };
            }
            catch (NullReferenceException ex)
            {
                string errorLine = "User " + userName + " encountered an exception during TryGetChampion in PopulationReadWrite: " + ex.Message;
                WriteErrorForDebug(errorLine);
            }
            return genome;
        }

        static void CreateFolderIfNotExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        static public void CreateCandidateVideoFromResults(string candidateName, string userName)
        {
            string commonPath = UserDefaultFolder(userName);
            string extractPath = commonPath + candidateName;
            string tarPath = commonPath + candidateName + ".tgz";
            string targetPath = GetCandidatePathFromName(candidateName, userName);
            // Overwrite in the extraction does not seem to work (maybe it only works for files with the same name,
            // but merges folders with the same name if the operation is repeated).
            DeleteFolderIfExists(extractPath);
            ExtractTar(tarPath, extractPath);
            MoveExtractedVideo(extractPath, targetPath);
        }

        static public void MoveSavedFilesToCandidateFolder(string currentFolder, string userName)
        {
            string defaultTargetFolderPath = UserDefaultFolder(userName);
            string sourcePath = currentFolder + "/_Population.xml";
            string targetPath = defaultTargetFolderPath + "_Population.xml";
            DeleteFolderIfExistsAndNew(defaultTargetFolderPath);
            //System.Diagnostics.Debug.WriteLine("Base directory " + projectBasePath);
            //System.Diagnostics.Debug.WriteLine("Moving from: " + currentFolder + "/_Population.xml" + " to: " + targetPath);
            MoveOneSavedFileToCandidate(currentFolder + "/_Population.xml", targetPath);
            sourcePath = currentFolder + "/_Champion.xml";
            targetPath = defaultTargetFolderPath + "_Champion.xml";
            MoveOneSavedFileToCandidate(currentFolder + "/_Champion.xml", targetPath);
        }

        static private void MoveOneSavedFileToCandidate(string currentPath, string targetPath)
        {
            string rootPath = Path.GetFullPath(Path.Combine(projectBasePath, @".\Views"));
            currentPath = rootPath + currentPath;
            DeleteFileIfExists(targetPath);
            File.Copy(currentPath, targetPath);
        }

        static public void DeleteLocalEvolutionFiles(string userName)
        {
            string localFolderPath = UserDefaultFolder(userName);
            DeleteFolderIfExistsAndNew(localFolderPath);
        }

        static void DeleteFolderIfExistsAndNew(string folderPath)
        {
            DeleteFolderIfExists(folderPath);
            Directory.CreateDirectory(folderPath);
        }

        static public void DeleteSavedFiles(string relativeFolderPath)
        {
            System.Diagnostics.Debug.WriteLine("Deleting from folder: " + relativeFolderPath);
            string completeFolderPath = "";
            string rootPath = Path.GetFullPath(Path.Combine(projectBasePath, @".\Views"));
            completeFolderPath = rootPath + relativeFolderPath;
            System.Diagnostics.Debug.WriteLine("Final folder: " + relativeFolderPath);
            DeleteFolderIfExists(completeFolderPath);
        }

        static private void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// This deletes the folder and any subfolders and files.
        /// </summary>
        static private void DeleteFolderIfExists(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }
        }

        static private void ExtractTar(string tarPath, string extractPath)
        {
            using (Stream stream = File.OpenRead(tarPath))
            {
                //System.Diagnostics.Debug.WriteLine("tarPath: " + tarPath);
                var reader = ReaderFactory.Open(stream);
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        reader.WriteEntryToDirectory(extractPath, new ExtractionOptions() { ExtractFullPath = true, Overwrite = true });
                    }
                }
            }
        }

        static private void MoveExtractedVideo(string extractPath, string targetPath)
        {
            string currentVideoPath = "";
            // Gets file and folder names
            //string[] fileEntries = Directory.GetFileSystemEntries(extractPath);
            // Returns only folder names
            string[] fileEntries = Directory.GetDirectories(extractPath);
            foreach (string fileName in fileEntries)
            {
                // There should only be one result... ensure?
                currentVideoPath = fileName + "\\video.mp4";
            }
            // Deletes the target in case it exists (before moving the video)
            DeleteFileIfExists(targetPath);
            // TODO: Protect against missing paths
            try
            {
                File.Move(currentVideoPath, targetPath);
            }
            catch (Exception ex)
            {
                WriteErrorForDebug("MoveExtractedVideo failed: " + ex.Message);
            }
        }

        static public void DeleteVideo(string candidateName, string userName)
        {
            string videoPath = GetCandidatePathFromName(candidateName, userName);
            DeleteFileIfExists(videoPath);
        }

        static private string GetCandidatePathFromName(string candidateName, string userName)
        {
            string folderPath;
            if (isRelease)
            {
                folderPath = projectBasePath + "\\VideoDatabase\\Candidates\\" + userName;
            }
            else
            {
                folderPath = projectBasePath + "VideoDatabase\\Candidates\\" + userName;
            }
            CreateFolderIfNotExists(folderPath);
            return folderPath + "\\" + candidateName + ".mp4";
        }

        static public void SaveToDatabase(string subFolderName, string currentVideoName, string userName)
        {
            System.Diagnostics.Debug.WriteLine("Asking to save: ");
            System.Diagnostics.Debug.WriteLine("Subfoldername: " + subFolderName + ", video name: " + currentVideoName + ", user: " + userName);
            
            string folderName = projectBasePath + "VideoDatabase\\" + subFolderName + "\\";
            //DeleteFolderIfExists(folderName); // SavePopulationAtPath creates the folder again 
            string videoPath = folderName + "video.mp4";
            SavePopulationAtPath(folderName, userName);
            MoveCandidateVideoFile(currentVideoName, videoPath, userName);
            // TODO: If any of the elements was NOT successful, notify!
        }

        /// <summary>
        /// When the user wants to load an existing genome the video is moved to the folder, but we need
        /// to rename it. If, for some reason, this video is missing, the program will create a new one,
        /// but for this we need to return "false". It is not good practice to mix return values and
        /// other tasks of the method.
        /// </summary>
        /// <param name="candidateIndex"></param>
        /// <returns></returns>
        static public bool RenameLoadedCandidate(int candidateIndex, string userName)
        {
            //FIX THIS IS CURRENTLY DOOMEND, SINCE WE ARE LACKING THE NAME OF THE SAVED CANDIDATE (ON TOP OF THE USERNAME)

            // Note we use candidateIndex + 1 because video files start counting from 1.
            string sourcePath = "";
            string targetPath = "";
            if (isRelease)
            {
                sourcePath = projectBasePath + "\\VideoDatabase\\Candidates\\" + userName + "\\video.mp4";
                targetPath = GetCandidatePathFromName("Candidate" + (candidateIndex + 1).ToString(), userName);
            }
            else
            {
                sourcePath = projectBasePath + "VideoDatabase\\Candidates\\" + userName + "\\video.mp4";
                targetPath = GetCandidatePathFromName("Candidate" + (candidateIndex + 1).ToString(), userName);
            }
            if (!File.Exists(sourcePath))
            {
                return false;
            }
            File.Move(sourcePath, targetPath);
            return true;
        }

        static private void MoveCandidateVideoFile(string videoName, string targetPath, string userName)
        {
            string currentPath = projectBasePath + "\\VideoDatabase\\Candidates\\" + userName + "\\" + videoName;
            //File.Move(currentPath, targetPath);
            File.Copy(currentPath, targetPath);
        }

        static public void ChangeNameDatabaseElement()
        {

        }
    }
}
