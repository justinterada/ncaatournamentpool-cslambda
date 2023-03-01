using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NcaaTournamentPool.Pages;

public class IndexModel : PageModel
{
    public Player[] Players { get; set; }

    public void OnGet()
    {
        Players = CommonMethods.loadPlayersForLobby().Result;
    }
}