using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using tmsserver.Models;
using tmsserver.Data.Repositories;

namespace tmsserver.Controllers;

[ApiController]
[Route("api/tournaments/{tournamentId}/bracket")]
public class BracketController : ControllerBase
{
    private readonly ITournamentTeamRepository _teamRepository;
    private readonly ITournamentMatchRepository _matchRepository;
    private readonly ITournamentRepository _tournamentRepository;
    private readonly IMatchScoreRepository _scoreRepository;
    private readonly ILiveGameScoreRepository _liveGameScoreRepository;

    public BracketController(
        ITournamentTeamRepository teamRepository,
        ITournamentMatchRepository matchRepository,
        ITournamentRepository tournamentRepository,
        IMatchScoreRepository scoreRepository,
        ILiveGameScoreRepository liveGameScoreRepository)
    {
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _scoreRepository = scoreRepository;
        _liveGameScoreRepository = liveGameScoreRepository;
    }

    // ==================== TEAMS ENDPOINTS ====================

    /// <summary>
    /// GET all teams for a tournament
    /// </summary>
    [HttpGet("teams")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TournamentTeam>>> GetTeams(int tournamentId)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            var teams = await _teamRepository.GetTeamsByTournamentAsync(tournamentId);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving teams: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET specific team by ID
    /// </summary>
    [HttpGet("teams/{teamId}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentTeam>> GetTeam(int tournamentId, int teamId)
    {
        try
        {
            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            if (team == null || team.TournamentId != tournamentId)
                return NotFound(new { message = "Team not found" });

            return Ok(team);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving team: {ex.Message}" });
        }
    }

    /// <summary>
    /// POST create new team for tournament
    /// </summary>
    [HttpPost("teams")]
    [Authorize]
    public async Task<ActionResult<TournamentTeam>> CreateTeam(int tournamentId, [FromBody] CreateTeamRequest request)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            if (string.IsNullOrWhiteSpace(request.TeamName))
                return BadRequest(new { message = "Team name is required" });

            // Get next order
            var existingTeams = await _teamRepository.GetTeamsByTournamentAsync(tournamentId);
            var nextOrder = existingTeams.Count > 0 ? existingTeams.Max(t => t.TeamOrder) + 1 : 0;

            var team = new TournamentTeam
            {
                TournamentId = tournamentId,
                TeamName = request.TeamName.Trim(),
                TeamOrder = nextOrder
            };

            var createdTeam = await _teamRepository.CreateTeamAsync(team);
            return CreatedAtAction(nameof(GetTeam), new { tournamentId, teamId = createdTeam.Id }, createdTeam);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error creating team: {ex.Message}" });
        }
    }

    /// <summary>
    /// PUT update team information
    /// </summary>
    [HttpPut("teams/{teamId}")]
    [Authorize]
    public async Task<ActionResult> UpdateTeam(int tournamentId, int teamId, [FromBody] UpdateTeamRequest request)
    {
        try
        {
            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            if (team == null || team.TournamentId != tournamentId)
                return NotFound(new { message = "Team not found" });

            if (!string.IsNullOrWhiteSpace(request.TeamName))
                team.TeamName = request.TeamName.Trim();

            if (request.TeamOrder.HasValue)
                team.TeamOrder = request.TeamOrder.Value;

            var updated = await _teamRepository.UpdateTeamAsync(team);
            if (!updated)
                return StatusCode(500, new { message = "Failed to update team" });

            return Ok(new { message = "Team updated successfully", team });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error updating team: {ex.Message}" });
        }
    }

    /// <summary>
    /// DELETE remove team from tournament
    /// </summary>
    [HttpDelete("teams/{teamId}")]
    [Authorize]
    public async Task<ActionResult> DeleteTeam(int tournamentId, int teamId)
    {
        try
        {
            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            if (team == null || team.TournamentId != tournamentId)
                return NotFound(new { message = "Team not found" });

            // Also delete all matches involving this team
            var allMatches = await _matchRepository.GetMatchesByTournamentAsync(tournamentId);
            var teamMatches = allMatches.Where(m => m.Team1Id == teamId || m.Team2Id == teamId).ToList();
            foreach (var match in teamMatches)
            {
                await _matchRepository.DeleteMatchAsync(match.Id);
            }

            var deleted = await _teamRepository.DeleteTeamAsync(teamId);
            if (!deleted)
                return StatusCode(500, new { message = "Failed to delete team" });

            return Ok(new { message = "Team deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error deleting team: {ex.Message}" });
        }
    }

    // ==================== MATCHES ENDPOINTS ====================

        /// <summary>
        /// GET all set scores for a match
        /// </summary>
        [HttpGet("matches/{matchId}/scores")]
        [AllowAnonymous]
        public async Task<ActionResult<List<MatchScore>>> GetMatchScores(int tournamentId, int matchId)
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });
            var scores = await _scoreRepository.GetScoresForMatchAsync(matchId);
            return Ok(scores);
        }

        /// <summary>
        /// PUT update set score for a match (admin only)
        /// </summary>
        [HttpPut("matches/{matchId}/scores/{setNumber}")]
        [Authorize]
        public async Task<ActionResult> UpdateMatchSetScore(int tournamentId, int matchId, int setNumber, [FromBody] UpdateSetScoreRequest request)
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });
            var score = new MatchScore
            {
                MatchId = matchId,
                SetNumber = setNumber,
                Team1Games = request.Team1Games,
                Team2Games = request.Team2Games,
                Team1TieBreak = request.Team1TieBreak,
                Team2TieBreak = request.Team2TieBreak
            };
            await _scoreRepository.AddOrUpdateScoreAsync(score);
            return Ok(new { message = "Set score updated", score });
        }

        /// <summary>
        /// DELETE remove set score for a match (admin only)
        /// </summary>
        [HttpDelete("matches/{matchId}/scores/{setNumber}")]
        [Authorize]
        public async Task<ActionResult> DeleteMatchSetScore(int tournamentId, int matchId, int setNumber)
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });
            
            var deleted = await _scoreRepository.DeleteScoreAsync(matchId, setNumber);
            if (!deleted)
                return StatusCode(500, new { message = "Failed or already deleted" });
            
            return Ok(new { message = "Set score deleted" });
        }

        /// <summary>
        /// GET live game score for a match
        /// </summary>
        [HttpGet("matches/{matchId}/live")]
        [AllowAnonymous]
        public async Task<ActionResult<LiveGameScore>> GetLiveGameScore(int tournamentId, int matchId)
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });
            var score = await _liveGameScoreRepository.GetLiveScoreAsync(matchId);
            if (score == null)
                return Ok(new LiveGameScore { MatchId = matchId });
            return Ok(score);
        }

        /// <summary>
        /// PUT update live game score for a match (admin only)
        /// </summary>
        [HttpPut("matches/{matchId}/live")]
        [Authorize]
        public async Task<ActionResult> UpdateLiveGameScore(int tournamentId, int matchId, [FromBody] UpdateLiveGameScoreRequest request)
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });
            var score = new LiveGameScore
            {
                MatchId = matchId,
                Team1Points = request.Team1Points,
                Team2Points = request.Team2Points,
                ServingTeamId = request.ServingTeamId,
                UpdatedAt = DateTime.UtcNow
            };
            await _liveGameScoreRepository.SetLiveScoreAsync(score);
            return Ok(new { message = "Live game score updated", score });
        }

    /// <summary>
    /// GET all matches for tournament
    /// </summary>
    [HttpGet("matches")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TournamentMatch>>> GetMatches(int tournamentId)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            var matches = await _matchRepository.GetMatchesByTournamentAsync(tournamentId);
            return Ok(matches);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving matches: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET regular matches only (non-playoff)
    /// </summary>
    [HttpGet("matches/regular")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TournamentMatch>>> GetRegularMatches(int tournamentId)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            var matches = await _matchRepository.GetRegularMatchesByTournamentAsync(tournamentId);
            return Ok(matches);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving matches: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET playoff matches only
    /// </summary>
    [HttpGet("matches/playoff")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TournamentMatch>>> GetPlayoffMatches(int tournamentId)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            var matches = await _matchRepository.GetPlayoffMatchesByTournamentAsync(tournamentId);
            return Ok(matches);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving playoff matches: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET specific match by ID
    /// </summary>
    [HttpGet("matches/{matchId}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentMatch>> GetMatch(int tournamentId, int matchId)
    {
        try
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });

            return Ok(match);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error retrieving match: {ex.Message}" });
        }
    }

    /// <summary>
    /// POST create new match
    /// </summary>
    [HttpPost("matches")]
    [Authorize]
    public async Task<ActionResult<TournamentMatch>> CreateMatch(int tournamentId, [FromBody] CreateMatchRequest request)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            // Validate teams exist
            var team1 = await _teamRepository.GetTeamByIdAsync(request.Team1Id);
            var team2 = await _teamRepository.GetTeamByIdAsync(request.Team2Id);

            if (team1 == null || team1.TournamentId != tournamentId)
                return BadRequest(new { message = "Team 1 not found in this tournament" });

            if (team2 == null || team2.TournamentId != tournamentId)
                return BadRequest(new { message = "Team 2 not found in this tournament" });

            if (request.Team1Id == request.Team2Id)
                return BadRequest(new { message = "Teams cannot play against themselves" });

            var match = new TournamentMatch
            {
                TournamentId = tournamentId,
                Team1Id = request.Team1Id,
                Team2Id = request.Team2Id,
                IsPlayoff = request.IsPlayoff ?? false
            };

            var createdMatch = await _matchRepository.CreateMatchAsync(match);
            return CreatedAtAction(nameof(GetMatch), new { tournamentId, matchId = createdMatch.Id }, createdMatch);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error creating match: {ex.Message}" });
        }
    }

    /// <summary>
    /// PUT create multiple matches at once (for initializing bracket)
    /// </summary>
    [HttpPost("matches/batch")]
    [Authorize]
    public async Task<ActionResult<List<TournamentMatch>>> CreateMatchesBatch(int tournamentId, [FromBody] List<CreateMatchRequest> requests)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            var createdMatches = new List<TournamentMatch>();

            foreach (var request in requests)
            {
                var team1 = await _teamRepository.GetTeamByIdAsync(request.Team1Id);
                var team2 = await _teamRepository.GetTeamByIdAsync(request.Team2Id);

                if (team1 == null || team1.TournamentId != tournamentId || 
                    team2 == null || team2.TournamentId != tournamentId ||
                    request.Team1Id == request.Team2Id)
                {
                    continue; // Skip invalid teams
                }

                var match = new TournamentMatch
                {
                    TournamentId = tournamentId,
                    Team1Id = request.Team1Id,
                    Team2Id = request.Team2Id,
                    IsPlayoff = request.IsPlayoff ?? false
                };

                var createdMatch = await _matchRepository.CreateMatchAsync(match);
                createdMatches.Add(createdMatch);
            }

            return Ok(new { message = $"Created {createdMatches.Count} matches", matches = createdMatches });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error creating matches: {ex.Message}" });
        }
    }

    /// <summary>
    /// PUT update match result (set winner)
    /// </summary>
    [HttpPut("matches/{matchId}")]
    [Authorize]
    public async Task<ActionResult> UpdateMatch(int tournamentId, int matchId, [FromBody] UpdateMatchRequest request)
    {
        try
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });

            if (request.WinnerId.HasValue)
            {
                // Validate winner is one of the teams
                if (request.WinnerId != match.Team1Id && request.WinnerId != match.Team2Id)
                    return BadRequest(new { message = "Winner must be one of the participating teams" });

                match.WinnerId = request.WinnerId;
            }

            var updated = await _matchRepository.UpdateMatchAsync(match);
            if (!updated)
                return StatusCode(500, new { message = "Failed to update match" });

            return Ok(new { message = "Match updated successfully", match });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error updating match: {ex.Message}" });
        }
    }

    /// <summary>
    /// DELETE remove specific match
    /// </summary>
    [HttpDelete("matches/{matchId}")]
    [Authorize]
    public async Task<ActionResult> DeleteMatch(int tournamentId, int matchId)
    {
        try
        {
            var match = await _matchRepository.GetMatchByIdAsync(matchId);
            if (match == null || match.TournamentId != tournamentId)
                return NotFound(new { message = "Match not found" });

            var deleted = await _matchRepository.DeleteMatchAsync(matchId);
            if (!deleted)
                return StatusCode(500, new { message = "Failed to delete match" });

            return Ok(new { message = "Match deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error deleting match: {ex.Message}" });
        }
    }

    // ==================== BRACKET MANAGEMENT ENDPOINTS ====================

    /// <summary>
    /// DELETE entire bracket (teams and matches) for tournament
    /// </summary>
    [HttpDelete("")]
    [Authorize]
    public async Task<ActionResult> DeleteBracket(int tournamentId)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            await _matchRepository.DeleteAllMatchesByTournamentAsync(tournamentId);
            await _teamRepository.DeleteAllTeamsByTournamentAsync(tournamentId);

            return Ok(new { message = "Bracket deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error deleting bracket: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET calculated standings for tournament
    /// </summary>
    [HttpGet("standings")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetStandings(int tournamentId)
    {
        try
        {
            var tournament = await _tournamentRepository.GetTournamentByIdAsync(tournamentId);
            if (tournament == null)
                return NotFound(new { message = "Tournament not found" });

            var teams = await _teamRepository.GetTeamsByTournamentAsync(tournamentId);
            var allMatches = await _matchRepository.GetMatchesByTournamentAsync(tournamentId);

            var standings = teams.Select(team => new
            {
                team.Id,
                team.TeamName,
                Wins = allMatches.Count(m => m.WinnerId == team.Id),
                Losses = allMatches.Count(m => 
                    (m.Team1Id == team.Id && m.WinnerId == m.Team2Id) ||
                    (m.Team2Id == team.Id && m.WinnerId == m.Team1Id)),
                TotalMatches = allMatches.Count(m => m.Team1Id == team.Id || m.Team2Id == team.Id)
            })
            .OrderByDescending(s => s.Wins)
            .ThenBy(s => s.Losses)
            .ToList();

            return Ok(standings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error calculating standings: {ex.Message}" });
        }
    }
}

// ==================== REQUEST/RESPONSE DTOs ====================

public class CreateTeamRequest
{
    public string TeamName { get; set; } = string.Empty;
}

public class UpdateTeamRequest
{
    public string? TeamName { get; set; }
    public int? TeamOrder { get; set; }
}

public class CreateMatchRequest
{
    public int Team1Id { get; set; }
    public int Team2Id { get; set; }
    public bool? IsPlayoff { get; set; }
}

public class UpdateMatchRequest
{
    public int? WinnerId { get; set; }
}

public class UpdateSetScoreRequest
{
    public int Team1Games { get; set; }
    public int Team2Games { get; set; }
    public int? Team1TieBreak { get; set; }
    public int? Team2TieBreak { get; set; }
}

public class UpdateLiveGameScoreRequest
{
    public string Team1Points { get; set; } = "0";
    public string Team2Points { get; set; } = "0";
    public int? ServingTeamId { get; set; }
}
