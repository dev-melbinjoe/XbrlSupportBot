using ClosedXML.Excel;
using XbrlSupportBot.Models;

namespace XbrlSupportBot.Services
{
    public class ExcelService
    {
        private readonly IConfiguration _config;

        public ExcelService(IConfiguration config)
        {
            _config = config;
        }

        public void Export(List<JiraTicket> tickets)
        {
            var path = _config["FilePaths:ExcelPath"];

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("RCA Data");

            ws.Cell(1, 1).Value = "Key";
            ws.Cell(1, 2).Value = "Summary";
            ws.Cell(1, 3).Value = "Priority";
            ws.Cell(1, 4).Value = "RCA";

            int row = 2;

            foreach (var t in tickets)
            {
                ws.Cell(row, 1).Value = t.Key;
                ws.Cell(row, 2).Value = t.Summary;
                ws.Cell(row, 3).Value = t.Priority;
                ws.Cell(row, 4).Value = t.RcaComment;
                row++;
            }

            workbook.SaveAs(path);
        }
    }
}