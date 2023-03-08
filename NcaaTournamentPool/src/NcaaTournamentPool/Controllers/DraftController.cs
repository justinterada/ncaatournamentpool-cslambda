using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace NcaaTournamentPool.Controllers
{
    [Route("api/[controller]")]
    public class DraftController : Controller
    {
        // GET: api/draft
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/draft/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            return "value";
        }

        // POST api/draft
        [HttpPost]
        public IActionResult Post([FromBody]DraftSelection selection)
        {
            CurrentStatus currentStatus = CommonMethods.loadCurrentStatus().Result;

            if (selection.CurrentUser != currentStatus.currentUserId
                || selection.CurrentRound != currentStatus.round)
            {
                return BadRequest("Invalid draft state");
            }

            Team[] selectedTeams = CommonMethods.loadTeams(selection.SelectedTeams).Result;

            int pointTotal = 0;

            foreach (Team team in selectedTeams)
            {
                if (team.pickedByPlayer != 0)
                {
                    return BadRequest(string.Format("{0} has already been chosen.", team.teamName));
                }

                pointTotal += team.cost;
            }

            int pointsAvailable = currentStatus.currentRound.pointsToSpend + currentStatus.currentPlayer.pointsHeldOver;

            if (pointTotal > pointsAvailable)
            {
                return BadRequest("Cannot afford these teams");
            }
            else if ((pointsAvailable - pointTotal) > currentStatus.currentRound.maxHoldOverPoints)
            {
                return BadRequest(string.Format("Cannot hold over more than {0} points.", currentStatus.currentRound.maxHoldOverPoints));
            }

            int pointsHeldOver = pointsAvailable - pointTotal;
            CommonMethods.advanceDraft(selectedTeams, currentStatus, pointsHeldOver);
            return AcceptedAtAction(null);
        }

        [HttpPost("runlottery")]
        public IActionResult RunLottery()
        {
            CommonMethods.runEndOfDraftLottery();

            return AcceptedAtAction(null);
        }

        [HttpPost("resetdraft")]
        public IActionResult ResetDraft()
        {
            CommonMethods.resetDraft();

            return AcceptedAtAction(null);
        }

        // PUT api/draft/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/draft/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}

