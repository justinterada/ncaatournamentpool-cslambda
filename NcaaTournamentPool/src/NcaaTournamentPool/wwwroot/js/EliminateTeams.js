function onReady(eventObject) {
    $("li[id^=\"team-selector-\"]").each(function(index) {
        var $current = $(this);
        var sCurveRank = parseInt($current.attr("id").replace("team-selector-", ""));

        var team = allTeams[sCurveRank - 1];
        $current.data("team", team);

        if (team.eliminated) {
            $current.addClass("eliminated");
            currentStatus.eliminateTeam(sCurveRank);
        }

        $current.css("cursor", "pointer");
        $current.click(toggleSelectTeam);
    });
}

function toggleSelectTeam(eventObject) {
    var $targetSelector = $(eventObject.target);

    if (eventObject.target.nodeName == "SPAN") {
        $targetSelector = $targetSelector.parent();
    }

    var team = $targetSelector.data("team");

    if (currentStatus.isTeamEliminated(team.sCurveRank))
    {
        $targetSelector.removeClass("eliminated");
        currentStatus.uneliminateTeam(team.sCurveRank);
    }
    else {
        $targetSelector.addClass("eliminated");
        currentStatus.eliminateTeam(team.sCurveRank);
    }

    return false;
}

function submit() {
    let url = 'api/draft/eliminateteams';
    let data = {
        'eliminatedTeams': currentStatus.eliminatedTeams
    };

    fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(data),
    }).then(res => {
        if (res.ok) {
            alert("Success!");
        } else {
            alert("Something went wrong");
        }
    });
}

var currentStatus =
{
    "eliminatedTeams": [],
    "eliminateTeam": function(sCurveRank) {
        for (var i = 0; i < this.eliminatedTeams.length; i++) {
            if (this.eliminatedTeams[i] == sCurveRank) {
                return;
            }
        }

        this.eliminatedTeams.push(sCurveRank);
    },
    "uneliminateTeam": function(sCurveRank) {
        var existingIndex = -1;

        for (var i = 0; i < this.eliminatedTeams.length; i++) {
            if (this.eliminatedTeams[i] == sCurveRank) {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0) {
            this.eliminatedTeams.splice(existingIndex, 1);
        }
    },
    "isTeamEliminated" : function(sCurveRank)
    {
        for (var i = 0; i < this.eliminatedTeams.length; i++) {
            if (this.eliminatedTeams[i] == sCurveRank) {
                return true;
            }
        }
        
        return false;
    }
};