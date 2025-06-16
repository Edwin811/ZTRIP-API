using Npgsql;
using System.Data;

namespace Z_TRIP.Helpers
{
    public class SqlDBHelper : IDisposable
    {
        private readonly NpgsqlConnection _connection;
        private bool _disposed;

        public SqlDBHelper(string connectionString)
        {
            _connection = new NpgsqlConnection(connectionString);
        }

        public NpgsqlCommand GetNpgsqlCommand(string query)
        {
            if (_connection.State != ConnectionState.Open)
                _connection.Open();

            var cmd = new NpgsqlCommand
            {
                Connection = _connection,
                CommandText = query,
                CommandType = CommandType.Text
            };

            return cmd;
        }

        // Proper method name with capital C
        public void CloseConnection()
        {
            if (_connection.State == ConnectionState.Open)
            {
                _connection.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        _connection.Close();
                    }
                    _connection.Dispose();
                }
                _disposed = true;
            }
        }
    }
}