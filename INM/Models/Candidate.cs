using System.Collections.Generic;
using INM.Controllers.Events;
using INM.Controllers;

namespace INM.Models
{
    public class Candidate
    {
        public int CandidateID { get; set; }
        public string FolderPath { get; set; }
        public string VideoPath { get; set; }
        public string Name { get; set; }
        public string UserName { get; set; }
        public string UserSignature { get; set; }
        public int? ParentID { get; set; }
        public List<int> ChildrenList { get; set; }
        public string UnparsedTags { get; set; }
        public List<string> Tags { get; set; }
        public string Description { get; set; }

        public Candidate()
        {            
            VideoPath = "";
            Name = "DefaultName";
            UserName = "DefaultUser";
            UserSignature = "DefaultSignature";
            ParentID = null;
            ChildrenList = new List<int>();
            UnparsedTags = "";
            Tags = new List<string>();
            Description = "";
        }

        public void RemoveWhiteSpaces()
        {
            Name = Name.Replace(" ", "_");
        }

        public void LinkNameAndPath(string nameOfUser)
        {
            if (SharpNeat.PopulationReadWrite.IsRelease)
            {
                // RELEASE
                FolderPath = "http://crowdai.itu.dk/VideoDatabase/" + nameOfUser + "/" + Name;
                VideoPath = FolderPath + "/video.mp4";
            }
            else
            {
                //DEBUG
                FolderPath = "../../VideoDatabase/" + nameOfUser + "/" + Name;
                VideoPath = FolderPath + "/video.mp4";
            }
        }

        public void CreatePathsForEvolutionCandidate(string userName, string candidateName)
        {
            if (SharpNeat.PopulationReadWrite.IsRelease)
            {
                //System.Diagnostics.Debug.WriteLine("Candidate path, RELEASE settings.");
                FolderPath = "http://crowdai.itu.dk/VideoDatabase/Candidates/" + userName;            
                VideoPath = FolderPath + "/" + candidateName + ".mp4";
            }
            else
            {
                //System.Diagnostics.Debug.WriteLine("Candidate path, DEBUG settings. Username: " + userName);
                FolderPath = "../../VideoDatabase/Candidates/" + userName;
                VideoPath = FolderPath + "/" + candidateName + ".mp4";
            }
            Name = candidateName;
        }

        public void AddChild(int childID)
        {
            // FIX
            System.Diagnostics.Debug.WriteLine("YES, YES, YES, Adding  " + childID);
            ChildrenList.Add(childID);
            System.Diagnostics.Debug.WriteLine("YES, YES, YES, Adding 1, 2, 3");
            ChildrenList.Add(1);
            ChildrenList.Add(2);
            ChildrenList.Add(3);
            //Name += "EDITADO";
        }

        public void ParseTags()
        {
            ParseTags(UnparsedTags);
        }
        void ParseTags(string unparsedTags)
        {
            // FIX
            Tags = new List<string>();
            string[] tags = unparsedTags.Split(',');
            foreach(string tag in tags)
            {
                System.Diagnostics.Debug.WriteLine("Adding tag:  " + tag);
                Tags.Add(tag);
            }
            /*
            there are gotchas with this - but ultimately the simplest way will be to use
            string s = [yourlongstring];
            string[] values = s.Split(',');
            If the number of commas and entries isn't important, and you want to get rid of 'empty' values then you can use
            string[] values = s.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            One thing, though - this will keep any whitespace before and after your strings. You could use a bit of Linq magic to solve that:
            string[] values = s.Split(',').Select(sValue => sValue.Trim()).ToArray();
            That's if you're using .Net 3.5 and you have the using System.Linq declaration at the top of your source file.
            */
        }
    }
}
 