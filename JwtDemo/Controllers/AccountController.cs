using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using AutoMapper;
using JwtDemo.Helpers;
using JwtDemo.Services;
using JwtDemo.ViewModel;
using JwtDemo.Persistence;
using JwtDemo.Models;
using JwtDemo.Models.Entities;



namespace JwtDemo.Controllers
{
    [Route("api/[controller]")]
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly UserManager<AppUser> _userManager;
        private readonly IJwtFactory _jwtFactory;
        private readonly JwtIssuerOptions _jwtOptions;
        private readonly IMapper _mapper;

        public AccountController(UserManager<AppUser> userManager, IJwtFactory jwtFactory, IOptions<JwtIssuerOptions> jwtOptions, IMapper mapper, ApplicationDbContext appDbContext)
        {
            _userManager = userManager;
            _mapper = mapper;
            _jwtFactory = jwtFactory;
            _jwtOptions = jwtOptions.Value;
            _appDbContext = appDbContext;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsync([FromBody]RegistrationViewModel model)
        {
            if (!ModelState.IsValid) {
                return BadRequest(ModelState);
            }

            var userIdentity = _mapper.Map<AppUser>(model);

            var result = await _userManager.CreateAsync(userIdentity, model.Password);

            if (!result.Succeeded) {
                return BadRequest(result.Errors);
            }

            await _appDbContext.Customers.AddAsync(new Customer { IdentityId = userIdentity.Id, Location = model.Location });
            await _appDbContext.SaveChangesAsync();

            var identity = await GetClaimsIdentity(model.Email, model.Password);

            var jwt = await Tokens.GenerateJwt(identity, _jwtFactory, model.Email, _jwtOptions, new JsonSerializerSettings { Formatting = Formatting.Indented });
            return Ok(jwt);
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody]CredentialsViewModel credentials)
        {
            if (!ModelState.IsValid) {
                return BadRequest(ModelState);
            }

            var identity = await GetClaimsIdentity(credentials.UserName, credentials.Password);
            if (identity == null) {
                return BadRequest("Invalid username or password.");
            }

            var jwt = await Tokens.GenerateJwt(identity, _jwtFactory, credentials.UserName, _jwtOptions, new JsonSerializerSettings { Formatting = Formatting.Indented });
            return Ok(jwt);
        }

        private async Task<ClaimsIdentity> GetClaimsIdentity(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
                return await Task.FromResult<ClaimsIdentity>(null);

            // get the user to verifty
            var userToVerify = await _userManager.FindByNameAsync(userName);

            if (userToVerify == null) return await Task.FromResult<ClaimsIdentity>(null);

            // check the credentials
            if (await _userManager.CheckPasswordAsync(userToVerify, password)) {
                return await Task.FromResult(_jwtFactory.GenerateClaimsIdentity(userName, userToVerify.Id));
            }

            // Credentials are invalid, or account doesn't exist
            return await Task.FromResult<ClaimsIdentity>(null);
        }
    }
}
