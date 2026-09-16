using System.Data;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;

public class PotatoProc
{
    static void SendOutput(string text)
    {
        SqlDataRecord rec = new SqlDataRecord(
            new SqlMetaData("output", SqlDbType.NVarChar, -1));
        SqlContext.Pipe.SendResultsStart(rec);
        rec.SetString(0, text);
        SqlContext.Pipe.SendResultsRow(rec);
        SqlContext.Pipe.SendResultsEnd();
    }

    [SqlProcedure]
    public static void Exec(SqlString cmd)
    {
        string result = Potato.Run(cmd.Value);
        SendOutput(result);
    }

    [SqlProcedure]
    public static void ExecWith(SqlString cmd, SqlString technique)
    {
        string result = Potato.Run(cmd.Value, technique.Value);
        SendOutput(result);
    }
}
