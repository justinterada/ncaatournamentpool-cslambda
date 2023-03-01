namespace NcaaTournamentPool
{

	public class Team
	{
        public int sCurveRank;
		public string teamName;
		public int wins;
		public int losses;
		public bool eliminated;
		public int rank;
        public int cost;
		public string bracket;
		public int bracketId;
		public int pickedByPlayer;
	}

	public class Player
	{
		public string name;
		public int userId;
		public string color;
		public int pointsAvailable;
		public int pointsHeldOver;
		public int initialPickOrder;
	}

	public class CurrentStatus
	{
		public int round;
		public int currentUserId;
		public int currentOrderIndex;
		public int maxHoldOverPoints;
		public int pointsToSpend;
		public int totalPlayers;
		public int totalRounds;
		public bool finished;
	}

	public class Round
	{
		public int roundNumber;
		public int[] roundOrder;
		public int pointsToSpend;
		public int maxHoldOverPoints;
	}
}
