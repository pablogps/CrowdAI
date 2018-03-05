using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using INM.Models;
using INM.Controllers.Events;
using Evolution.UsersInfo;
using SharpNeat.Genomes.Neat;

namespace INM.Controllers
{
    public class CandidatesController : Controller
    {
        private INMContext db = new INMContext();
        private List<Candidate> candidates;

        void HeartBeat(string userName)
        {
            // Rest timer already checks for the user
            ActiveUsersList<NeatGenome>.ResetTimer(userName);
        }
        
        void WriteLineForDebug(string line, string userName)
        {
            EventsController.WriteLineForDebug(line, userName);
        }

        public ActionResult UnexpectedError()
        {
            return View();
        }

        public ActionResult EvolutionBusy()
        {
            return View();
        }

        // Shows the candidate videos after each generation
        public ActionResult Index()
        {
            string userName = HttpContext.UserIdentity();
            PrepareCandidateModels();
            if (!CheckIfUserExists(userName))
            {
                return RedirectToAction("UnexpectedError");
            }
            UpdateCandidatePaths(userName);
            System.Diagnostics.Debug.WriteLine("Number of candidates: " + candidates.Count);
            return View(candidates);
        }
        void PrepareCandidateModels()
        {
            candidates = new List<Candidate>();
            for (int i = 0; i < ActiveUsersList<NeatGenome>.PopulationSize; ++i)
            {
                candidates.Add(new Candidate());
            }
        }
        private void UpdateCandidatePaths(string userName)
        {
            // With the user name we find the EvolutionAlgorithm in ActiveUsersList, we access 
            // GenomeScreenOrder in the evolution alg. and there we get the IndexToName dictionary.
            // The user existence has just been confirmed
            Dictionary<int, string> indexToName =
                    ActiveUsersList<NeatGenome>.EvolutionAlgorithmForUser(userName).GenomeScreenOrder.IndexToName;
            for (int index = 0; index < candidates.Count; ++index)
            {
                string candidateName = "";
                if (indexToName.ContainsKey(index))
                {
                    candidateName = indexToName[index];
                }
                candidates[index].CreatePathsForEvolutionCandidate(userName, candidateName);
            }
        }

        private bool CheckIfUserExists(string userName)
        {
            if (ActiveUsersList<NeatGenome>.ContainsUser(userName))
            {
                return true;
            }
            return false;
        }

        public ActionResult StartEvolution()
        {
            string userName = HttpContext.UserIdentity();
            WriteLineForDebug("Requesting to start evolution!", userName);
            //Malmo.MinimalMalmo minimalMalmoInstance = new Malmo.MinimalMalmo();
            //minimalMalmoInstance.Initialize();
            //minimalMalmoInstance.PingMalmo();
            if (EventsController.RaiseStartEvolutionEvent(userName))
            {
                return RedirectToAction("WaitingForEvolutionSetup");
            }
            else
            {
                return RedirectToAction("EvolutionBusy");
            }
        }

        public ActionResult ResetEvolution()
        {
            string userName = HttpContext.UserIdentity();
            if (CheckIfUserExists(userName))
            {
                WriteLineForDebug("Reset evolution", userName);
                // The user is active: send a heartbeat
                HeartBeat(userName);
                // If we were branching from a saved genome, then the user had that
                // genome as the parent ID. Reseting evolution means this is no longer
                // the parent (there is none) so we set this ID to null
                ActiveUsersList<NeatGenome>.AddParentToUserName(userName, null);
                ActiveUsersList<NeatGenome>.SetUserWaitingForVideos(userName);
                EventsController.RaiseResetEvolutionEvent(userName);
                return RedirectToAction("WaitingForEvolutionSetup");
            }
            else
            {
                return RedirectToAction("Index");
            }
        }

        public ActionResult StopEvolution()
        {
            string userName = HttpContext.UserIdentity();
            if (CheckIfUserExists(userName))
            {
                ActiveUsersList<NeatGenome>.SetUserWaitingForVideos(userName);
                EventsController.RaiseStopEvolutionEvent(userName);
                return RedirectToAction("Index", "Home");
            }
            else
            {
                return RedirectToAction("Index");
            }
        }
        
        public ActionResult WaitingForEvolutionSetup()
        {
            return View();
        }

        public ActionResult NextGeneration(int candidateIndex, bool isNormalMutations)
        {
            string userName = HttpContext.UserIdentity();
            // The user is active: send a heartbeat
            HeartBeat(userName);
            string line = NextGenerationLineForLog(candidateIndex.ToString(), isNormalMutations);
            WriteLineForDebug(line, userName);
            ActiveUsersList<NeatGenome>.SetUserWaitingForVideos(userName);
            EventsController.RaiseNextGenerationEvent(candidateIndex, userName, isNormalMutations);
            return RedirectToAction("WaitingForCandidateVideosDisplay");
        }

        string NextGenerationLineForLog(string index, bool isNormalMutations)
        {
            if (isNormalMutations)
            {
                return "Mutation: normal, for candidate " + index;
            }
            else
            {
                return "Mutation: large, for candidate " + index;
            }
        }

        public ActionResult WaitingForCandidateVideosDisplay()
        {
            return View();
        }

        // GET: Pictures
        public ActionResult HomePage()
        {
            // This takes all pictures from the database
            var allPictures = db.Candidates.ToList();
            return View(allPictures);
        }
        
        // GET: Pictures
        public ActionResult ShowSavedCandidates()
        {
            // This takes all pictures from the database
            var allPictures = db.Candidates.ToList();
            return View(allPictures);
        }

        // GET: Pictures
        public ActionResult ControlRoom()
        {
            ViewBag.Message = "Secret control page.";
            // This takes all pictures from the database
            var allPictures = db.Candidates.ToList();
            return View(allPictures);
        }

        public ActionResult Branch(int? id)
        {
            string userName = HttpContext.UserIdentity();
            WriteLineForDebug("Branching from candidate " + id.ToString(), userName);
            if (id == null)
            {
                WriteLineForDebug("ID was wrong when branching.", userName);
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Candidate candidate = db.Candidates.Find(id);
            if (candidate == null)
            {
                WriteLineForDebug("Candidate not found when branchin.", userName);
                return HttpNotFound();
            }
            if (!EventsController.IsFreeEvolSlot(userName))
            {
                return RedirectToAction("EvolutionBusy");
            }
            PrepareAndRaiseBranch(id, candidate, userName);
            return RedirectToAction("WaitingForEvolutionSetup");
        }

        /// <summary>
        /// This process takes into account the posibility that the evolution algorithm has not been
        /// started yet. The user is pre-registered, and the save files are moved to the correct folder, 
        /// so that THE new evolution process will automatically read them.
        /// </summary>
        private void PrepareAndRaiseBranch(int? id, Candidate candidate, string userName)
        {
            ActiveUsersList<NeatGenome>.AddParentToUserName(userName, id);
            ActiveUsersList<NeatGenome>.SetUserWaitingForAlgorithm(userName);
            ActiveUsersList<NeatGenome>.SetUserWaitingForVideos(userName);
            SharpNeat.PopulationReadWrite.MoveSavedFilesToCandidateFolder(candidate.FolderPath, userName);
            EventsController.RaiseBranchEvent(userName);
        }

        // GET: Pictures/Details/5
        public ActionResult Details(int? id)
        {
            System.Diagnostics.Debug.WriteLine("Child ID: " + id.ToString());
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Candidate candidate = db.Candidates.Find(id);
            if (candidate == null)
            {
                return HttpNotFound();
            }
            return View(candidate);
        }

        // GET: Pictures/Create
        //public ActionResult Create(string someData)
        public ActionResult Create()
        {
            return View();
        }

        // POST: Pictures/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        // Note that PictureID will be handled automatically!
        [HttpPost]
        [ValidateAntiForgeryToken]        
        public ActionResult Create([Bind(Include =
                "CandidateID, Name, UserName, UserSignature, UnparsedTags, Description," +
                "ChildrenList, Tags")] Candidate candidate, int candidateIndex)
        {
            string user = HttpContext.UserIdentity();
            if (ModelState.IsValid && candidate.Name != null)
            {
                if (CheckIfUserExists(user))
                {
                    candidate.RemoveWhiteSpaces();
                    string folderName = user + "\\" + candidate.Name;
                    EventsController.RaiseSaveCandidate(candidateIndex, folderName, user);
                    candidate.ParentID = ActiveUsersList<NeatGenome>.UserParent(user);
                    candidate.UserName = user;
                    candidate.LinkNameAndPath(user);
                    candidate.ParseTags();
                    db.Candidates.Add(candidate);
                    UpdateParentsChildren(candidate.ParentID, candidate.CandidateID);
                    db.SaveChanges();
                    return RedirectToAction("ShowSavedCandidates");
                }
            }
            return View(candidate);
        }

        void UpdateParentsChildren(int? parentIDnullable, int childID)
        {
            // Even after checking parentIDnullable != null
            // int parentID = parentIDnullable gives an error.
            // v2 = v1 ?? default(int); equals: v2 = v1 == null ? default(int) : v1;
            int parentID = parentIDnullable ?? 100000;
            Candidate parent = db.Candidates.Find(parentID);
            if (parent != null)
            {
                //System.Diagnostics.Debug.WriteLine("Adding  " + childID + " to " + parentID);
                parent.AddChild(childID);
                db.SaveChanges();
            }
        }

        // GET: Pictures/Edit/5
        public ActionResult Edit(int? id)
        {
            // TODO: Check user name!
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Candidate candidate = db.Candidates.Find(id);
            if (candidate == null)
            {
                return HttpNotFound();
            }
            return View(candidate);
        }

        // POST: Pictures/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "CandidateID, Name, UserName, UnparsedTags, Description")] Candidate candidate)
        {
            string userName = HttpContext.UserIdentity();
            if (ModelState.IsValid)
            {
                candidate.LinkNameAndPath(userName);
                candidate.ParseTags();
                db.Entry(candidate).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("ShowSavedCandidates");
            }
            return View(candidate);
        }

        // GET: Pictures/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Candidate candidate = db.Candidates.Find(id);
            if (candidate == null)
            {
                return HttpNotFound();
            }
            return View(candidate);
        }

        // POST: Pictures/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Candidate candidate = db.Candidates.Find(id);
            SharpNeat.PopulationReadWrite.DeleteSavedFiles(candidate.FolderPath);
            db.Candidates.Remove(candidate);
            db.SaveChanges();
            return RedirectToAction("ControlRoom", "Candidates");
        }
        
        public ActionResult ExportDatabase()
        {
            DatabaseExporter.PrintCandidatesToFile(db.Candidates);
            return RedirectToAction("ControlRoom", "Candidates");
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
