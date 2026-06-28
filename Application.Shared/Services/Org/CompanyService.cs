using Application.Shared.Data;
using Application.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace Application.Shared.Services.Org
{
    public class CompanyService : ICompanyService
    {
        private readonly IDbContextFactory<UserManagementDbContext> _contextFactory;

        public CompanyService(IDbContextFactory<UserManagementDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }


        public async Task<List<Company>> GetCompanies(string userId)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            var companyMembersList = await _context.CompanyMember.Where(m => m.ApplicationUserId == userId).ToListAsync();
            var companyMembers = companyMembersList.Select(m => m.CompanyId).ToArray();

            return await _context.Company.Where(c => companyMembers.Contains(c.Id)).ToListAsync();
        }
        public async Task<Company> GetCompany(string id)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            return (await _context.Company.FindAsync(id))!;
        }

        public async Task<Company> GetCompany(string id, string userId)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            var companyMembersList = await _context.CompanyMember.Where(m => m.ApplicationUserId == userId).ToListAsync();
            var companyMembers = companyMembersList.Select(m => m.CompanyId).ToArray();

            return (await _context.Company.FirstOrDefaultAsync(c => c.Id == id && companyMembers.Contains(c.Id)))!;

        }


        public async Task<bool> UserIsCompanyMember(string companyId, string userId)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            return await _context.CompanyMember.AnyAsync(m => m.CompanyId == companyId && m.ApplicationUserId == userId);
        }


        public async Task<CompanyMember> AddCompanyMember(string companyId, string userId)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            var companyMember = new CompanyMember
            {
                CompanyId = companyId,
                ApplicationUserId = userId
            };

            _context.CompanyMember.Add(companyMember);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.CompanyMember.AnyAsync(m => m.CompanyId == companyMember.CompanyId && m.ApplicationUserId == companyMember.ApplicationUserId))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }

            return companyMember;
        }


        public async Task<CompanyMember> AddCompanyMemberByDomain(string domain, string userId)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            // get the first company with the domain
            var company = await _context.CompanyDomain.FirstOrDefaultAsync(d => d.Domain == domain);

            if (company == null)
            {
                return null!;
            }

            var companyMember = new CompanyMember
            {
                CompanyId = company.CompanyId,
                ApplicationUserId = userId
            };

            _context.CompanyMember.Add(companyMember);
            await _context.SaveChangesAsync();

            return companyMember;
        }

        public async Task<Company> CreateCompany(Company company, string userId)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();

            company.CreatedBy = userId;
            company.ModifiedBy = userId;
            company.CreatedOn = DateTime.Now;
            company.ModifiedOn = DateTime.Now;
            company.IsDeleted = false;

            _context.Company.Add(company);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.Company.AnyAsync(e => e.Id == company.Id))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }

            // Add the creator as a company member
            await AddCompanyMember(company.Id!, userId);

            return company;
        }



        //public async Task<Company> PutCompany(string id, Company Company)
        //{
        //    if (id != Company.Id)
        //    {
        //        return null;
        //    }

        //    _context.Entry(Company).State = EntityState.Modified;

        //    try
        //    {
        //        await _context.SaveChangesAsync();
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        if (!CompanyExists(id))
        //        {
        //            return null;
        //        }
        //        else
        //        {
        //            throw;
        //        }
        //    }

        //    return Company;
        //}




        //public async Task<Company> DeleteCompany(string id)
        //{
        //    var Company = await _context.Company.FindAsync(id);

        //    if (Company == null)
        //    {
        //        return null;
        //    }

        //    _context.Company.Remove(Company);
        //    await _context.SaveChangesAsync();

        //    return null;
        //}




    }
}
