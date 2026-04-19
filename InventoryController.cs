using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using tmsserver.Models;
using System;
using System.Threading.Tasks;
using tmsserver.Data.Repositories;

namespace tmsserver
{
    [ApiController]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryRepository _repository;
        private readonly IUserRepository _userRepository;

        public InventoryController(IInventoryRepository repository, IUserRepository userRepository)
        {
            _repository = repository;
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory()
        {
            try
            {
                var items = await _repository.GetAllItemsAsync();
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("add")]
        public async Task<ActionResult> AddItem([FromBody] InventoryItem item)
        {
            try
            {
                var created = await _repository.AddItemAsync(item);
                return Ok(created);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<ActionResult> DeleteItem(int id)
        {
            try
            {
                bool deleted = await _repository.DeleteItemAsync(id);
                if (!deleted) return NotFound();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        public class UpdateConditionRequest
        {
            public string? Condition { get; set; }
        }

        [HttpPut("condition/{id}")]
        public async Task<ActionResult> UpdateCondition(int id, [FromBody] UpdateConditionRequest req)
        {
            try
            {
                bool updated = await _repository.UpdateItemConditionAsync(id, req.Condition);
                if (!updated) return NotFound();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("transactions")]
        public async Task<ActionResult<IEnumerable<InventoryTransaction>>> GetTransactions()
        {
            try
            {
                var txs = await _repository.GetTransactionsAsync(returnsOnly: false);
                return Ok(txs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("issue")]
        public async Task<ActionResult> IssueItem([FromBody] IssueRequest req)
        {
            try
            {
                bool success = await _repository.IssueItemAsync(req);
                if (!success) return BadRequest("Failed to issue item (check stock or existence)");
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("request")]
        public async Task<ActionResult> RequestItem([FromBody] RequestItemRequest req)
        {
            try
            {
                bool success = await _repository.RequestItemAsync(req);
                if (!success) return BadRequest("Failed to request item (check stock or existence)");
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("return/{transactionId}")]
        public async Task<ActionResult> ReturnTransaction(int transactionId)
        {
            try
            {
                bool success = await _repository.ReturnTransactionAsync(transactionId);
                if (!success) return BadRequest("Failed to return item (check transaction)");
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("returned-transactions")]
        public async Task<ActionResult<IEnumerable<InventoryTransaction>>> GetReturnedTransactions()
        {
            try
            {
                var txs = await _repository.GetTransactionsAsync(returnsOnly: true);
                return Ok(txs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("user/{id}")]
        public async Task<ActionResult<User>> GetUserById(int id)
        {
            try
            {
                var user = await _userRepository.GetUserByIdAsync(id);
                if (user == null) return NotFound();
                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("user-by-username/{username}")]
        public async Task<ActionResult<User>> GetUserByUsername(string username)
        {
            try
            {
                var user = await _userRepository.GetUserByUsernameAsync(username);
                if (user == null) return NotFound();
                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
