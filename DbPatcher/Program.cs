using Npgsql;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Avixar.DbPatcher;

class Program
{
    static async Task Main(string[] args)
    {
        await SampleDataSeeder.Run();
    }
}
