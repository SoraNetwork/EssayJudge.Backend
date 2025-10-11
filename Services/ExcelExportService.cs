using Microsoft.EntityFrameworkCore;
using SoraEssayJudge.Data;
using SoraEssayJudge.Models;
using SoraEssayJudge.Models.DTOs;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SoraEssayJudge.Services
{
    public class ExcelExportService : IExcelExportService
    {
        private readonly EssayContext _context;

        public ExcelExportService(EssayContext context)
        {
            _context = context;
        }

        public async Task<byte[]> ExportEssaySubmissionsAsync(ExportFilterDto? filter = null)
        {
            var submissions = await GetFilteredSubmissions(filter);

            using var memoryStream = new MemoryStream();
            using (var spreadsheet = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
            {
                // Add a WorkbookPart to the document.
                var workbookpart = spreadsheet.AddWorkbookPart();
                workbookpart.Workbook = new Workbook();

                // Add Styles
                var stylesPart = workbookpart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = CreateStylesheet();
                stylesPart.Stylesheet.Save();

                // Add a WorksheetPart to the WorkbookPart.
                var worksheetPart = workbookpart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();
                worksheetPart.Worksheet = new Worksheet(sheetData);

                // Add Sheets to the Workbook.
                var sheets = workbookpart.Workbook.AppendChild(new Sheets());

                // Append a new worksheet and associate it with the workbook.
                var sheet = new Sheet()
                {
                    Id = workbookpart.GetIdOfPart(worksheetPart),
                    SheetId = 1,
                    Name = "作文评分报告"
                };
                sheets.Append(sheet);

                // Add header row
                var headerRow = new Row();
                var headerCells = new[]
                {
                    "作文标题",
                    "班级",
                    "学生",
                    "得分",
                    "AI评分",
                    "AI评分结果",
                    "提交时间",
                    "查询码",
                    "来源"
                };

                foreach (var header in headerCells)
                {
                    var cell = new Cell()
                    {
                        DataType = CellValues.String,
                        CellValue = new CellValue(header),
                        StyleIndex = 1
                    };
                    headerRow.AppendChild(cell);
                }
                sheetData.AppendChild(headerRow);

                // Add data rows
                foreach (var submission in submissions)
                {
                    var dataRow = new Row();

                    // Get AI scoring information
                    var aiResults = submission.AIResults.ToList();
                    var aiScore = aiResults.Any() ?
                        string.Join("/", aiResults.Select(ai => ai.Score?.ToString() ?? "无")) : "无评分";
                    var aiFeedback = aiResults.Any() ?
                        string.Join("; ", aiResults.Select(ai => $"{ai.ModelName.Split('-')[0]}:{ai.Feedback}")) : "无反馈";

                    // Set row style based on score
                    uint rowStyleIndex = GetRowStyleIndex(submission.FinalScore ?? submission.Score);

                    var cellValues = new[]
                    {
                        submission.Title ?? "无标题",
                        submission.Student?.Class?.Name ?? "未知班级",
                        submission.Student?.Name ?? "未知学生",
                        submission.FinalScore?.ToString() ?? "N/A",
                        aiScore,
                        aiFeedback,
                        submission.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        submission.Id.ToString("N")[^8..],
                        submission.EssayAssignment?.Description ?? "未知"
                    };

                    foreach (var value in cellValues)
                    {
                        var cell = new Cell()
                        {
                            DataType = CellValues.String,
                            CellValue = new CellValue(value),
                            StyleIndex = rowStyleIndex
                        };
                        dataRow.AppendChild(cell);
                    }
                    sheetData.AppendChild(dataRow);
                }

                // Set column widths
                var columns = new Columns();
                var columnWidths = new[] { 25.0, 15.0, 15.0, 15.0, 15.0, 40.0, 20.0, 12.0, 15.0 };
                for (uint i = 0; i < columnWidths.Length; i++)
                {
                    columns.Append(new Column()
                    {
                        Min = i + 1,
                        Max = i + 1,
                        Width = columnWidths[i],
                        CustomWidth = true
                    });
                }
                worksheetPart.Worksheet.InsertBefore(columns, sheetData);

                // Save the worksheet
                worksheetPart.Worksheet.Save();
                workbookpart.Workbook.Save();
            }

            // Reset the stream position before returning
            memoryStream.Position = 0;
            return memoryStream.ToArray();
        }

        public Task<byte[]> ExportEssaySubmissionsWithDetailsAsync(ExportFilterDto? filter = null)
        {
            return ExportEssaySubmissionsAsync(filter);
        }

        private async Task<List<EssaySubmission>> GetFilteredSubmissions(ExportFilterDto? filter)
        {
            var query = _context.EssaySubmissions
                .Include(es => es.Student)
                    .ThenInclude(s => s.Class)
                .Include(es => es.AIResults)
                .Include(es => es.EssayAssignment)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.EssayAssignmentId.HasValue)
                {
                    query = query.Where(es => es.EssayAssignmentId == filter.EssayAssignmentId.Value);
                }

                if (filter.EssayAssignmentIds != null && filter.EssayAssignmentIds.Any())
                {
                    query = query.Where(es => filter.EssayAssignmentIds.Contains(es.EssayAssignmentId));
                }

                if (filter.ClassId.HasValue)
                {
                    query = query.Where(es => es.Student.ClassId == filter.ClassId.Value);
                }

                if (filter.StartDate.HasValue)
                    query = query.Where(es => es.CreatedAt >= filter.StartDate.Value);

                if (filter.EndDate.HasValue)
                    query = query.Where(es => es.CreatedAt <= filter.EndDate.Value);
            }

            return await query
                .OrderByDescending(es => es.CreatedAt)
                .ToListAsync();
        }

        private Stylesheet CreateStylesheet()
        {
            return new Stylesheet(
                new Fonts(
                    new Font(), // Index 0 - Default font
                    new Font( // Index 1 - Bold font (header)
                        new Bold(),
                        new FontSize() { Val = 11 },
                        new Color() { Rgb = new HexBinaryValue("000000") }
                    )
                ),
                new Fills(
                    new Fill(new PatternFill() { PatternType = PatternValues.None }), // Index 0 - No fill
                    new Fill(new PatternFill() { PatternType = PatternValues.Gray125 }), // Index 1 - Default gray
                    new Fill(new PatternFill( // Index 2 - Header blue
                        new ForegroundColor() { Rgb = new HexBinaryValue("FF4F81BD") }
                    ) { PatternType = PatternValues.Solid }),
                    new Fill(new PatternFill( // Index 3 - Alternate row light gray
                        new ForegroundColor() { Rgb = new HexBinaryValue("FFF2F2F2") }
                    ) { PatternType = PatternValues.Solid }),
                    new Fill(new PatternFill( // Index 4 - High score green
                        new ForegroundColor() { Rgb = new HexBinaryValue("FFC6EFCE") }
                    ) { PatternType = PatternValues.Solid }),
                    new Fill(new PatternFill( // Index 5 - Medium score yellow
                        new ForegroundColor() { Rgb = new HexBinaryValue("FFFFEB9C") }
                    ) { PatternType = PatternValues.Solid }),
                    new Fill(new PatternFill( // Index 6 - Low score red
                        new ForegroundColor() { Rgb = new HexBinaryValue("FFFFC7CE") }
                    ) { PatternType = PatternValues.Solid })
                ),
                new Borders(
                    new Border( // Index 0 - No border
                        new LeftBorder(),
                        new RightBorder(),
                        new TopBorder(),
                        new BottomBorder(),
                        new DiagonalBorder()
                    )
                ),
                new CellFormats(
                    new CellFormat() { FontId = 0, FillId = 0, BorderId = 0 }, // Index 0 - Default style
                    new CellFormat() { // Index 1 - Header style
                        FontId = 1,
                        FillId = 2,
                        BorderId = 0,
                        ApplyFont = true,
                        ApplyFill = true
                    },
                    new CellFormat() { // Index 2 - Alternate row style
                        FontId = 0,
                        FillId = 3,
                        BorderId = 0,
                        ApplyFill = true
                    },
                    new CellFormat() { FontId = 0, FillId = 0, BorderId = 0 }, // Index 3 - Default white row
                    new CellFormat() { // Index 4 - High score style
                        FontId = 0,
                        FillId = 4,
                        BorderId = 0,
                        ApplyFill = true
                    },
                    new CellFormat() { // Index 5 - Medium score style
                        FontId = 0,
                        FillId = 5,
                        BorderId = 0,
                        ApplyFill = true
                    },
                    new CellFormat() { // Index 6 - Low score style
                        FontId = 0,
                        FillId = 6,
                        BorderId = 0,
                        ApplyFill = true
                    }
                )
            );
        }

        private uint GetRowStyleIndex(double? score)
        {
            if (!score.HasValue) return 3; // Default white

            return score.Value switch
            {
                >= 48 => 4, // Green - Excellent
                >= 42 => 5, // Yellow - Good
                >= 38 => 6, // Red - Pass
                _ => 6      // Red - Fail
            };
        }
    }
}
