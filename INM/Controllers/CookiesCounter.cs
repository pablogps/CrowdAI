using System;
using System.IO;

namespace INM.Controllers
{
    static public class CookiesCounter
    {
        static private int latestUsedId;
        //const string path = "../LatestID.txt";
        static string path;
        static CookiesCounter()
        {
            path = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.GetFullPath(Path.Combine(path, @"..\LatestID.txt"));
            string message = "File path for latestID: " + path;
            latestUsedId = 0;
            LoadLatestID();
        }

        static public string ReturnNewestID()
        {
            ++latestUsedId;
            SaveLatestID();
            return latestUsedId.ToString();
        }
        
        static public void SaveLatestID()
        {
            string[] lines = { latestUsedId.ToString() };
            File.WriteAllLines(path, lines);
        }

        static public void LoadLatestID()
        {
            bool fileDidNotExist = EnsureFileExists();
            if (fileDidNotExist) { return; }
            string rawString = File.ReadAllText(path);
            if (IsFileEmpty(rawString)) { return; }
            int? loadedID = TryParseString(rawString);
            EnsureStoredIdGreaterThanGiven(loadedID);
        }

        static public void EnsureLatestIdIsCurrent(string visitorIDstring)
        {
            int? visitorIDint = TryParseString(visitorIDstring);
            EnsureStoredIdGreaterThanGiven(visitorIDint);
        }
        
        static bool EnsureFileExists()
        {
            bool idLoaded = false;
            if (!File.Exists(path))
            {
                SaveLatestID();
                idLoaded = true;
            }
            return idLoaded;
        }

        static bool IsFileEmpty(string rawString)
        {
            bool wasEmpty = false;
            if (rawString == null || rawString == "")
            {
                wasEmpty = true;
            }
            return wasEmpty;
        }

        static int? TryParseString(string rawString)
        {
            try
            {
                int parsedNumber = Int32.Parse(rawString);
                return parsedNumber;
            }
            catch (FormatException e)
            {
                string error = "Error parsing rawString in CookiesCounter: " + e.Message;
                string specialUserName = "0";
                Events.EventsController.WriteLineForDebug(error, specialUserName);
            }
            int? parseResultNull = null;
            return parseResultNull;
        }

        static void EnsureStoredIdGreaterThanGiven(int? givenID)
        {
            if (givenID == null)
            {
                return;
            }
            // if(v1==null) { v2 = default(int); } else { v2 = v1; }
            // That can be written as: v2 = v1 ?? default(int);
            if (givenID > latestUsedId)
            {
                latestUsedId = givenID ?? default(int);
                SaveLatestID();
            }
        }
    }
}