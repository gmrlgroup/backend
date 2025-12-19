using Application.Scheduler.Models;
using Application.Shared.Data;
using Application.Shared.Models.Data;
using Hangfire.Console;
using Hangfire.Server;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Application.Scheduler.Repositories;

public interface IDatabaseRepository
{
    Task<List<Database>> GetDatabaseDetails();

    Task<List<Database>> GetNokNokDatabaseDetails();
}


public class DatabaseRepository : IDatabaseRepository
{
    private readonly ApplicationDbContext _context;

    public DatabaseRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Database>> GetDatabaseDetails()
    {
        return _context.Database
            .Where(d => d.DatabaseType == "RBO")
            .ToList();
    }

    public async Task<List<Database>> GetNokNokDatabaseDetails()
    {
        return _context.Database
            .Where(d => d.DatabaseType == "NKDB")
            .ToList();
    }
}

