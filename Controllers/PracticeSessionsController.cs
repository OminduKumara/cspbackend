using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using tmsserver.Data.Repositories;
using tmsserver.Models;

namespace tmsserver.Controllers
{
    // This sets the base URL for these endpoints to: http://localhost:[port]/api/practicesessions
    [Route("api/[controller]")]
    [ApiController]
    public class PracticeSessionsController : ControllerBase
    {
        private readonly PracticeSessionRepository _repository;

        // We pull the configuration to pass the connection string down to the repository
        public PracticeSessionsController(IConfiguration configuration)
        {
            _repository = new PracticeSessionRepository(configuration);
        }

        // 1. GET: api/practicesessions
        // React uses this to fetch and display the schedule
        [HttpGet]
        public ActionResult<IEnumerable<PracticeSession>> Get()
        {
            var sessions = _repository.GetAllSessions();
            return Ok(sessions);
        }

        // 2. POST: api/practicesessions
        // Admin dashboard uses this to create a new session
        [HttpPost]
        public ActionResult Post([FromBody] PracticeSession session)
        {
            _repository.AddSession(session);
            return Ok(new { message = "Practice session added successfully!" });
        }

        // 3. PUT: api/practicesessions/5
        // Admin dashboard uses this to update an existing session
        [HttpPut("{id}")]
        public ActionResult Put(int id, [FromBody] PracticeSession session)
        {
            session.Id = id; // Ensure the ID from the URL matches the data
            _repository.UpdateSession(session);
            return Ok(new { message = "Practice session updated successfully!" });
        }

        // 4. DELETE: api/practicesessions/5
        // Admin dashboard uses this to remove a session
        [HttpDelete("{id}")]
        public ActionResult Delete(int id)
        {
            _repository.DeleteSession(id);
            return Ok(new { message = "Practice session deleted successfully!" });
        }
    }
}