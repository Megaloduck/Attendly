using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Attendly.Data;
using Attendly.Models;

namespace Attendly.Desktop.Export;

/// <summary>
/// Exports one or every Tartil's monthly attendance in the exact column order verified
/// against the real July 2026 workbook: NO / NIK / NAMA / NAMA PANGGILAN /
/// TARTIL / HADIR / S / I / A / [one column per day of the month].
/// A null TartilLevel means "Semua Kelas" - every class together (one sheet per class
/// in Excel, all rows appended in the CSV, distinguished by the TARTIL column).
/// Desktop/admin-only - not part of the mobile teacher clients.
/// </summary>
public class AttendanceExportService
{
    private readonly AttendanceRepository _repository;

    public AttendanceExportService(AttendanceRepository repository)
    {
        _repository = repository;
    }

    private sealed record ExportRow(
        int No,
        Santri Santri,
        IReadOnlyDictionary<int, AttendanceStatus> StatusByDay,
        int Hadir, int Sakit, int Izin, int Alpha);

    private async Task<(int daysInMonth, List<ExportRow> rows)> BuildRowsAsync(TartilLevel level, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var roster = await _repository.GetSantriByTartilAsync(level);
        var records = await _repository.GetAttendanceForMonthAsync(level, year, month);
        var bySantri = records.ToLookup(r => r.SantriId);

        var rows = new List<ExportRow>();
        var no = 1;

        foreach (var santri in roster)
        {
            var statusByDay = new Dictionary<int, AttendanceStatus>();
            int hadir = 0, sakit = 0, izin = 0, alpha = 0;

            foreach (var record in bySantri[santri.Id])
            {
                statusByDay[record.Date.Day] = record.Status;
                switch (record.Status)
                {
                    case AttendanceStatus.Hadir: hadir++; break;
                    case AttendanceStatus.Sakit: sakit++; break;
                    case AttendanceStatus.Izin: izin++; break;
                    case AttendanceStatus.Alpha: alpha++; break;
                        // Libur is deliberately excluded - it isn't counted in HADIR/S/I/A
                        // in the real manual either.
                }
            }

            rows.Add(new ExportRow(no++, santri, statusByDay, hadir, sakit, izin, alpha));
        }

        return (daysInMonth, rows);
    }

    /// <summary>level == null exports every Tartil, one after another, into the same CSV -
    /// NO restarts at 1 for each class (matching the per-sheet numbering in the XLSX
    /// export), and the existing TARTIL column is what tells the classes apart.</summary>
    public async Task ExportCsvAsync(TartilLevel? level, int year, int month, string filePath)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var sb = new StringBuilder();
        var header = new List<string> { "NO", "NIK", "NAMA", "NAMA PANGGILAN", "TARTIL", "HADIR", "S", "I", "A" };
        header.AddRange(Enumerable.Range(1, daysInMonth).Select(d => d.ToString(CultureInfo.InvariantCulture)));
        sb.AppendLine(string.Join(",", header.Select(CsvEscape)));

        var levels = level is { } single ? new[] { single } : Enum.GetValues<TartilLevel>();

        foreach (var lvl in levels)
        {
            var (_, rows) = await BuildRowsAsync(lvl, year, month);

            foreach (var row in rows)
            {
                var fields = new List<string>
                {
                    row.No.ToString(CultureInfo.InvariantCulture),
                    row.Santri.Nik,
                    row.Santri.Nama,
                    row.Santri.NamaPanggilan,
                    lvl.ToDisplayString(),
                    row.Hadir.ToString(CultureInfo.InvariantCulture),
                    row.Sakit.ToString(CultureInfo.InvariantCulture),
                    row.Izin.ToString(CultureInfo.InvariantCulture),
                    row.Alpha.ToString(CultureInfo.InvariantCulture),
                };

                for (var day = 1; day <= daysInMonth; day++)
                    fields.Add(row.StatusByDay.TryGetValue(day, out var status) ? status.ToCode().ToString() : "");

                sb.AppendLine(string.Join(",", fields.Select(CsvEscape)));
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string CsvEscape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    /// <summary>level == null exports every Tartil into the same workbook, one sheet per
    /// class (via AddSheet below) - each sheet uses the exact layout a single-class export
    /// already produced.</summary>
    public async Task ExportXlsxAsync(TartilLevel? level, int year, int month, string filePath)
    {
        using var workbook = new XLWorkbook();

        var levels = level is { } single ? new[] { single } : Enum.GetValues<TartilLevel>();

        foreach (var lvl in levels)
        {
            var (daysInMonth, rows) = await BuildRowsAsync(lvl, year, month);
            AddSheet(workbook, lvl, daysInMonth, rows);
        }

        workbook.SaveAs(filePath);
    }

    /// <summary>Builds one class's sheet - fixed columns (NO..HADIR) vertically merged
    /// across the two header rows; ABSEN merged horizontally over S/I/A - mirrors the
    /// real manual's layout. Extracted out of ExportXlsxAsync so "Semua Kelas" can call
    /// this once per class against the same workbook.</summary>
    private static void AddSheet(XLWorkbook workbook, TartilLevel level, int daysInMonth, List<ExportRow> rows)
    {
        var rawSheetName = level.ToDisplayString();
        var sheetName = rawSheetName.Length > 31 ? rawSheetName[..31] : rawSheetName; // Excel's 31-char sheet-name limit
        var sheet = workbook.Worksheets.Add(sheetName);

        string[] fixedHeaders = { "NO", "NIK", "NAMA", "NAMA PANGGILAN", "TARTIL", "HADIR" };
        for (var col = 1; col <= fixedHeaders.Length; col++)
        {
            sheet.Cell(1, col).Value = fixedHeaders[col - 1];
            sheet.Range(1, col, 2, col).Merge();
        }

        var absenStartCol = fixedHeaders.Length + 1; // column 7
        sheet.Cell(1, absenStartCol).Value = "ABSEN";
        sheet.Range(1, absenStartCol, 1, absenStartCol + 2).Merge();
        sheet.Cell(2, absenStartCol).Value = "S";
        sheet.Cell(2, absenStartCol + 1).Value = "I";
        sheet.Cell(2, absenStartCol + 2).Value = "A";

        var dayStartCol = absenStartCol + 3; // column 10
        for (var day = 1; day <= daysInMonth; day++)
        {
            var col = dayStartCol + day - 1;
            sheet.Cell(1, col).Value = day;
            sheet.Range(1, col, 2, col).Merge();
        }

        var headerRange = sheet.Range(1, 1, 2, dayStartCol + daysInMonth - 1);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        var rowIndex = 3;
        foreach (var row in rows)
        {
            sheet.Cell(rowIndex, 1).Value = row.No;
            sheet.Cell(rowIndex, 2).Value = row.Santri.Nik;
            sheet.Cell(rowIndex, 3).Value = row.Santri.Nama;
            sheet.Cell(rowIndex, 4).Value = row.Santri.NamaPanggilan;
            sheet.Cell(rowIndex, 5).Value = level.ToDisplayString();
            sheet.Cell(rowIndex, 6).Value = row.Hadir;
            sheet.Cell(rowIndex, 7).Value = row.Sakit;
            sheet.Cell(rowIndex, 8).Value = row.Izin;
            sheet.Cell(rowIndex, 9).Value = row.Alpha;

            for (var day = 1; day <= daysInMonth; day++)
            {
                if (row.StatusByDay.TryGetValue(day, out var status))
                    sheet.Cell(rowIndex, dayStartCol + day - 1).Value = status.ToCode().ToString();
            }

            rowIndex++;
        }

        sheet.Columns(1, dayStartCol + daysInMonth - 1).AdjustToContents();
        sheet.SheetView.FreezeRows(2);
        sheet.SheetView.FreezeColumns(4);
    }
}