﻿using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VacunaAPI.DTOs;
using VacunaAPI.Entities;
using VacunaAPI.Utils;

namespace VacunaAPI.Controllers
{
    [Route("api/immunization")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ImmunizationsController : PsBaseController
    {
        private readonly string container = "ImmunizationCards";

        public ImmunizationsController(UserManager<ApplicationUser> userManager, IMapper mapper,
            IStorageSaver storageSaver, ApplicationDbContext context) :
            base(userManager, mapper, storageSaver, context)
        {
        }


        //[HttpPost("validateImmunization")]
        //public async Task<ActionResult<ImmunizationDTO>> ValidateImmunization()
        //{
        //    var user = await GetConectedUser();
        //    //TODO: Validate if have the subscription or rol to make this action

        //    var user = await GetConectedUser();
        //    var inmunizations = await context.Inmunizations.Where(p => p.UserId == user.Id).ToListAsync();
        //    return mapper.Map<List<InmunizationDTO>>(inmunizations);
        //}

        [HttpGet]
        public async Task<ActionResult<List<ImmunizationDTO>>> Get()
        {
            var user = await GetConectedUser();
            var inmunizations = await Context.Immunizations.Where(p => p.UserId == user.Id).ToListAsync();
            return Mapper.Map<List<ImmunizationDTO>>(inmunizations);
        }


        [HttpGet("getByUser")]
        public async Task<ActionResult<List<ImmunizationDTO>>> GetByUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await GetConectedUser();
            //TODO: Change this to false
            bool canCheckIt = true;// false;
                                   //  canCheckIt = await UserManager.IsInRoleAsync(user, "Admin");

            if (!canCheckIt)
                return Unauthorized();

            var inmunizations = await Context.Immunizations
                .Include(p => p.Vaccine)
                .Include(p => p.Laboratory)
                .Where(p => p.UserId == userId).ToListAsync();
            return Mapper.Map<List<ImmunizationDTO>>(inmunizations);
        }

        [HttpGet("getFullImmunization")]
        public async Task<ActionResult<FullImmunizationDTO>> GetFullImmunization(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await GetConectedUser();

            //TODO: Change this to false
            bool canCheckIt = true;// false;
            if (userId == user.Id)
                canCheckIt = true;
            //else
            //    canCheckIt = await UserManager.IsInRoleAsync(user, "Admin");

            if (!canCheckIt)
                return Unauthorized();

            var inmunizations = await Context.Immunizations
                .Include(p => p.Vaccine)
                .Include(p => p.Laboratory)
                .Where(p => p.UserId == userId).ToListAsync();

            var immunizationsDto = Mapper.Map<List<ImmunizationDTO>>(inmunizations);
            var userDto = Mapper.Map<UserDTO>(user);

            var fullimmunization = new FullImmunizationDTO();
            fullimmunization.Immunizations = immunizationsDto;
            fullimmunization.Person = userDto;

            return fullimmunization;
        }

        [HttpGet("getIfUserIsImmunizedWith")]
        public async Task<ActionResult<IsVaccinatedWithDTO>> GetIfUserIsImmunizedWith(string userId, [FromQuery] int[] vaccinesIds)
        {
            if (!(vaccinesIds.Length > 0))
                return BadRequest();
            if (string.IsNullOrEmpty(userId))
                return BadRequest();

            var user = await GetConectedUser();
            //TODO: Change this to false
            bool canCheckIt = true;// false;
            bool isImmunized = false;
            bool isValidated = false;

            //TODO: Enabled This
            //  canCheckIt = await UserManager.IsInRoleAsync(user, "Admin");

            if (!canCheckIt)
                return Unauthorized();

            var immunizations = await Context.Immunizations
                .Include(p => p.Vaccine)
                .Include(p => p.Laboratory)
                .Where(p => p.UserId == userId).ToListAsync();

            foreach (var vaccineId in vaccinesIds)
            {
                var immunization = immunizations.FirstOrDefault(p => p.VaccineId == vaccineId);
                if (immunization == null)
                {
                    isValidated = false;
                    isImmunized = false;
                    break;
                }
                else
                {
                    isValidated = immunization.WasValidated;
                    isImmunized = true;
                }
            }
            var dtos =  Mapper.Map<List<ImmunizationDTO>>(immunizations);
            var isVaccinated = new IsVaccinatedWithDTO
            {
                IsImmunized = isImmunized, IsValidated=isValidated, Immunizations = dtos
            };

            return Ok(isVaccinated);
        }

        [HttpGet("getById")]
        public async Task<ActionResult<ImmunizationDTO>> GetById(int id)
        {
            var user = await GetConectedUser();
            bool canCheckIt = false;

            var immunization = await Context.Immunizations
                .Include(p => p.Vaccine)
                .Include(p => p.Laboratory)
                .Where(p => p.Id == id).FirstOrDefaultAsync();

            if (immunization != null)
            {
                if (immunization.UserId != user.Id)
                {
                    canCheckIt = await UserManager.IsInRoleAsync(user, "Admin");
                    if (canCheckIt == false)
                        return Unauthorized();
                }
            }
            return Mapper.Map<ImmunizationDTO>(immunization);
        }


        [HttpPost]
        public async Task<ActionResult> Post([FromBody] ImmunizationCreationDTO model)
        {
            var user = await GetConectedUser();

            var immunization = Mapper.Map<Immunization>(model);
            if (model.Photo != null)
            {
                var cardPicture = new Image { UserId = user.Id, TypeId = 2 };
                cardPicture.ImageUrl = await StorageSaver.SaveFile(container, model.Photo);
                Context.Images.Add(cardPicture);
                await Context.SaveChangesAsync();
            }
            immunization.UserId = user.Id;

            //TODO: Here we have to make validation of the center if exist or something

            Context.Add(immunization);
            await Context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<ApplicationUser> GetConectedUser()
        {
            var email = HttpContext.User.Claims.FirstOrDefault(x => x.Type == "email")?.Value;
            if (string.IsNullOrEmpty(email))
                return new ApplicationUser();
            var user = await UserManager.FindByEmailAsync(email);
            return user;
        }

    }
}
