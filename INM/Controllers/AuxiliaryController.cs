using System;
using System.Web.Mvc;
using INM.Controllers.Events;
// For ActiveUsersList:
using Evolution.UsersInfo;
using SharpNeat.Genomes.Neat;


namespace INM.Controllers
{
    public class AuxiliaryController : AsyncController
    {
        public static event EventHandler<AuxiliaryEventsArgs> EvolutionNowReady;
        public static event EventHandler<AuxiliaryEventsArgs> CandidatesNowReady;
        private string userName = ""; 

        // NOTE: WaitDuringEvolutionSetupAsync and WaitForCandidatesAsync are basically
        // the same method. Unfortunatelly, it is not trivial to create a generic method
        // for these, as there is some stuff going on behind the scenes here (sende, AsyncManager...).
        // In the best case it would require many parameters (also bad practice) or 
        // wasting too much time for little.

        // Similarly, WaitDuringEvolutionSetupCompleted and WaitForCandidatesCompleted
        // would require to return RedirectToAction which would end up being too messy.

        public void WaitDuringEvolutionSetupAsync()
        {
            // UserName is used below
            userName = HttpContext.UserIdentity();

            AsyncManager.OutstandingOperations.Increment();
            EvolutionNowReady += (sender, e) =>
            {
                AsyncManager.Parameters["success"] = e.success;
                AsyncManager.OutstandingOperations.Decrement();
            };
            CheckEvolutionSetup();
        }

        public void WaitForCandidatesAsync()
        {
            // UserName is used below
            userName = HttpContext.UserIdentity();

            AsyncManager.OutstandingOperations.Increment();
            CandidatesNowReady += (sender, e) =>
            {
                AsyncManager.Parameters["success"] = e.success;
                AsyncManager.OutstandingOperations.Decrement();
            };
            CheckCandidatesReady();
        }

        public ActionResult WaitDuringEvolutionSetupCompleted(bool success)
        {
            if (success)
            {
                return RedirectToAction("WaitingForCandidateVideosDisplay", "Candidates");
            }
            else
            {
                return RedirectToAction("UnexpectedError", "Candidates");
            }
        }

        public ActionResult WaitForCandidatesCompleted(bool success)
        {
            if (success)
            {
                return RedirectToAction("Index", "Candidates");
            }
            else
            {
                return RedirectToAction("UnexpectedError", "Candidates");
            }
        }

        private void CheckEvolutionSetup()
        {
            WaitHere(EvolutionNowReady, WaitForSetup);
        }

        private void CheckCandidatesReady()
        {
            WaitHere(CandidatesNowReady, WaitForCandidates);
        }

        private void WaitHere(EventHandler<AuxiliaryEventsArgs> eventHandler, Func<bool> waitMethod)
        {
            bool isWaitSuccessful = waitMethod();
            if (null != eventHandler)
            {
                AuxiliaryEventsArgs arguments = new AuxiliaryEventsArgs();
                arguments.success = isWaitSuccessful;
                try
                {
                    eventHandler(null, arguments);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error in waiting event" + ex);
                }
            }
        }

        // Perhaps this code repetition can also be improved...

        private bool WaitForSetup()
        {
            int waitingLoops = 0;
            System.Diagnostics.Debug.WriteLine("user in wait for setup: " + userName);
            while (!ActiveUsersList<NeatGenome>.IsUsersEvolutionReady(userName))
            {
                System.Threading.Thread.Sleep(100);
                ++waitingLoops;
                if (waitingLoops > 200)
                {
                    System.Diagnostics.Debug.WriteLine("Waiting for evolution algorithm to be ready timed out!");
                    return false;
                }
            }
            return true;
        }        

        private bool WaitForCandidates()
        {
            int waitingLoops = 0;
            System.Diagnostics.Debug.WriteLine("user in wait for candidates: " + userName);
            while (ActiveUsersList<NeatGenome>.IsUserWaitingForVideos(userName))
            {
                System.Threading.Thread.Sleep(100);
                ++waitingLoops;
                // Remember: each loop takes about 0.1 seconds (see Thread.Sleep just above)
                if (waitingLoops > 350)
                {
                    System.Diagnostics.Debug.WriteLine("Waiting forcandidates to be ready timed out!");
                    return false;
                }
            }
            return true;
        }
        
        // Not used like this any longer.
        //Now some especific actions call HeartBeat (in CandidatesController).
        /*
        [HttpPost]
        public JsonResult ReceiveHeartbeat()
        {
            System.Diagnostics.Debug.WriteLine("This is a heart beat");
            ActiveUsersList<NeatGenome>.ResetTimer(IPstring());
            return new JsonResult { Data = "success" };
        }
        */
    }
}