using GPInventory.Application.Interfaces;
using GPInventory.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace GPInventory.Infrastructure.Repositories;

public class InstallmentDocumentRepository : IInstallmentDocumentRepository
{
    private readonly string _connectionString;

    public InstallmentDocumentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<InstallmentDocument?> GetByIdAsync(int id)
    {
        const string sql = @"
            SELECT id, payment_installment_id, file_name, file_path, file_type, 
                   file_size, notes, uploaded_at
            FROM installment_document
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapDocument(reader);
        }

        return null;
    }

    public async Task<IEnumerable<InstallmentDocument>> GetByInstallmentIdAsync(int installmentId)
    {
        var documents = new List<InstallmentDocument>();
        const string sql = @"
            SELECT id, payment_installment_id, file_name, file_path, file_type, 
                   file_size, notes, uploaded_at
            FROM installment_document
            WHERE payment_installment_id = @installmentId
            ORDER BY uploaded_at DESC";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@installmentId", installmentId);

        await connection.OpenAsync();
        
        Console.WriteLine($"[InstallmentDocumentRepository] Buscando documentos para installment ID: {installmentId}");
        
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var doc = MapDocument(reader);
            documents.Add(doc);
            Console.WriteLine($"[InstallmentDocumentRepository] Documento encontrado: ID={doc.Id}, FileName={doc.FileName}");
        }
        
        Console.WriteLine($"[InstallmentDocumentRepository] Total documentos encontrados: {documents.Count}");

        return documents;
    }

    public async Task<InstallmentDocument> CreateAsync(InstallmentDocument document)
    {
        const string sql = @"
            INSERT INTO installment_document 
            (payment_installment_id, file_name, file_path, file_type, file_size, notes, uploaded_at)
            VALUES 
            (@paymentInstallmentId, @fileName, @filePath, @fileType, @fileSize, @notes, @uploadedAt);
            SELECT LAST_INSERT_ID() AS id;";

        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@paymentInstallmentId", document.PaymentInstallmentId);
        command.Parameters.AddWithValue("@fileName", document.FileName);
        command.Parameters.AddWithValue("@filePath", document.FilePath);
        command.Parameters.AddWithValue("@fileType", document.FileType);
        command.Parameters.AddWithValue("@fileSize", document.FileSize);
        command.Parameters.AddWithValue("@notes", (object?)document.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@uploadedAt", document.UploadedAt);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync() && await reader.NextResultAsync() && await reader.ReadAsync())
        {
            document.Id = reader.GetInt32(0);
        }

        return document;
    }

    public async Task DeleteAsync(int id)
    {
        const string sql = @"
            DELETE FROM installment_document 
            WHERE id = @id";

        using var connection = new MySqlConnection(_connectionString);
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }

    private static InstallmentDocument MapDocument(MySqlDataReader reader)
    {
        return new InstallmentDocument
        {
            Id = reader.GetInt32(0),
            PaymentInstallmentId = reader.GetInt32(1),
            FileName = reader.GetString(2),
            FilePath = reader.GetString(3),
            FileType = reader.GetString(4),
            FileSize = reader.IsDBNull(5) ? 0 : Convert.ToInt64(reader.GetDouble(5)),
            Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
            UploadedAt = reader.GetDateTime(7)
        };
    }
}
