using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NcaaTournamentPool.Pages
{
	public class LobbyModel : PageModel
    {
        public CurrentStatus CurrentStatus { get; set; }
        public Round[] Rounds { get; set; }
        public Player[] Players { get; set; }

        public int UserId { get; set; }

        public void OnGet()
        {
            CurrentStatus = CommonMethods.loadCurrentStatus().Result;
            Rounds = CurrentStatus.rounds;
            Players = CurrentStatus.players;
            UserId = Convert.ToInt32(this.Request.Query["userId"]);
        }
    }
}
