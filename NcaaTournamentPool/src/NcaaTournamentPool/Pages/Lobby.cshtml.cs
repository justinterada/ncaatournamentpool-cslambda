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
        public CurrentStatus DraftStatus { get; set; }
        public Round[] Rounds { get; set; }
        public Player[] Players { get; set; }

        public string UserId { get; set; }

        public void OnGet()
        {
            DraftStatus = CommonMethods.loadCurrentStatus().Result;
            Rounds = DraftStatus.rounds;
            Players = DraftStatus.players;
            UserId = this.Request.Query["userId"];
        }
    }
}
