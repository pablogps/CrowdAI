using System;
using System.Collections.Generic;
using System.Xml;
using System.Data.Entity;
using INM.Models;

namespace INM.Controllers
{
    public static class DatabaseExporter
    {
        static DbSet<Candidate> candidates;
        static string databaseFilePath;
        static string elemRoot = "Root";
        static string elemController = "Controller";
        static string attrId = "ID";
        static string attrString = "String";
        static string attrTag = "Tag";
        static string elemFolder = "FolderPath";
        static string elemVideo = "VideoPath";
        static string elemName = "Name";
        static string elemUserSignature = "UserSignature";
        static string elemUser = "User";
        static string elemParent = "Parent";
        static string elemChildren = "ChildrenList";
        static string elemTags = "TagsList";
        static string elemDescription = "Description";

        static DatabaseExporter()
        {
            databaseFilePath = SharpNeat.PopulationReadWrite.GetEvolutionFolderPath();
            databaseFilePath += "DataBase.xml";
        }
        
        public static void PrintCandidatesToFile(DbSet<Candidate> candidatesInDatabase)
        {
            candidates = candidatesInDatabase;
            System.Diagnostics.Debug.WriteLine("Listing info for all candidates:\n");  
            XmlDocument databaseFile = CreateXmlFile();
            databaseFile.Save(databaseFilePath);
        }

        static XmlDocument CreateXmlFile()
        {
            XmlDocument databaseFile = new XmlDocument();
            using (XmlWriter writer = databaseFile.CreateNavigator().AppendChild())
            {
                WriteDatabase(writer);
            }
            return databaseFile;
        }

        static void WriteDatabase(XmlWriter writer)
        {
            writer.WriteStartElement(elemRoot);
            foreach (Candidate candidate in candidates)
            {
                writer.WriteStartElement(elemController);
                writer.WriteAttributeString(attrId, candidate.CandidateID.ToString());
                ElementWithOneAttribute(writer, elemFolder, attrString, candidate.FolderPath);
                ElementWithOneAttribute(writer, elemVideo, attrString, candidate.VideoPath);
                ElementWithOneAttribute(writer, elemName, attrString, candidate.Name);
                ElementWithOneAttribute(writer, elemUserSignature, attrString, candidate.UserSignature);
                ElementWithOneAttribute(writer, elemUser, attrString, candidate.UserName);
                ElementWithOneAttribute(writer, elemParent, attrId, candidate.ParentID.ToString());
                ElementList(writer, elemChildren, attrId, candidate.ChildrenList);
                ElementList(writer, elemTags, attrTag, candidate.Tags);
                ElementWithOneAttribute(writer, elemDescription, attrString, candidate.Description);
                writer.WriteEndElement();

                if (candidate.ChildrenList.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Many children here");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No children here");
                }
            }
            writer.WriteEndElement();
        }

        static void ElementWithOneAttribute(XmlWriter writer, string element,
                                            string attributeName, string attributeValue)
        {
            writer.WriteStartElement(element);
            writer.WriteAttributeString(attributeName, attributeValue);
            writer.WriteEndElement();
        }

        static void ElementList(XmlWriter writer, string element,
                                string attributeName, List<string> list)
        {
            if (list.Count != 0)
            {
                foreach (string listElement in list)
                {
                    writer.WriteStartElement(element);
                    writer.WriteAttributeString(attributeName, listElement);
                    writer.WriteEndElement();
                }
            }
            else
            {               
                writer.WriteStartElement(element);
                writer.WriteEndElement();
            }
        }
        static void ElementList(XmlWriter writer, string element,
                               string attributeName, List<int> list)
        {
            List<string> toStringList = new List<string>();
            foreach (int listElement in list)
            {
                toStringList.Add(listElement.ToString());
            }
            ElementList(writer, element, attributeName, toStringList);
        }
    }
}