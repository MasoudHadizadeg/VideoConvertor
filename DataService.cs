using System.Data.SqlClient;
using Dapper;
using VideoConverter.Classes;
using Yashil.Common.SharedKernel.Helpers;

namespace VideoConverter
{
    internal class DataService
    {
        private readonly string _connectionString;

        public DataService(string connectionString)
        {
            var connectionString = CryptographyHelper.AesDecrypt(_configuration.GetConnectionString("YashilAppDB"));

            _connectionString = connectionString;
        }

        public async Task<IEnumerable<DockerInfo>> GetVideosForConvertAsync()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Define your SQL query
                string query = "select a.Id,a.DocTypeId,a.OrginalName,a.Extension" +
                               "  from dms.AppDocument a " +
                               "  inner join dms.DocType dt " +
                               "  on a.DocTypeId = dt.Id";

                // Execute the query and map results to the Video model
                var videos = await connection.QueryAsync<DockerInfo>(query, new {  });

                return videos;
            }
        }
    }
}
}
