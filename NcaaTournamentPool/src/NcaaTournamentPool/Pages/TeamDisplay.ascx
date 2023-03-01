<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="TeamDisplay.ascx.cs" Inherits="NcaaTourneyPool.Controls.TeamDisplay" %>
<li class="<%= IsFirst ? "first" : "" %><%= (IsFirst && IsLast) ? " " : "" %><%= IsLast ? "last" : "" %>" <%= Color.HasValue ? ("style=\"background-color:" + System.Drawing.ColorTranslator.ToHtml(Color.Value) + "\"") : "" %> id="team-selector-<%= this.Team.sCurveRank %>"><%= this.Team.rank %>. <%= this.Team.teamName %> (<%= this.Team.wins %> - <%= this.Team.losses %>)<%= this.Team.pickedByPlayer == 0 ? string.Format(" <span class=\"cost\">{0}</span>", this.Team.cost.ToString()) : "" %></li>