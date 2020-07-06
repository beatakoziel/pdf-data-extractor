using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PDFDataExtractor
{
    class PDFParser
    {
        private static string[] linesFromSimpleExtractor;
        private static string[] linesFromLocationExtractor;
        private static ITextExtractionStrategy simpleExtractionStrategy = new SimpleTextExtractionStrategy();
        private static ITextExtractionStrategy locationExtractionStrategy = new LocationTextExtractionStrategy();

        public static string PdfText(string path)
        {
            PdfReader reader = new PdfReader(path);
            string textFromSimpleExtractor = "", textFromLocationExtractor = "";

            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                textFromSimpleExtractor += PdfTextExtractor.GetTextFromPage(reader, page, simpleExtractionStrategy);
                textFromLocationExtractor += PdfTextExtractor.GetTextFromPage(reader, page, locationExtractionStrategy);
            }

            linesFromSimpleExtractor = textFromSimpleExtractor.Split('\n');
            linesFromLocationExtractor = textFromLocationExtractor.Split('\n');
            reader.Close();
            //PrintArray(linesFromSimpleExtractor);
            FindNIPNumber(linesFromSimpleExtractor);
            FindDeliveryDate(linesFromLocationExtractor);
            FindInvoiceNumber(linesFromLocationExtractor);
            FindDateOfPayment(linesFromSimpleExtractor);
            FindDateOfIssue(linesFromLocationExtractor);
            return textFromSimpleExtractor;
        }

        private static void PrintArray(string[] array)
        {
            foreach (string n in array)
            {
                Console.WriteLine(n);
            }
        }

        public static void FindDateOfIssue(string[] PDFLines)
        {
            int indexOfIssueDate = Array.FindIndex(PDFLines, line => line.Contains("data wystawienia", StringComparison.OrdinalIgnoreCase));
            if (indexOfIssueDate < 0)
                indexOfIssueDate = Array.FindIndex(PDFLines, line => line.Contains("wystawiono", StringComparison.OrdinalIgnoreCase));
            string date = GetDateFromLine(PDFLines[indexOfIssueDate]);

            if (date.Equals("--"))
                date = GetDateFromLine(PDFLines[indexOfIssueDate - 1]);
            if (date.Equals("--"))
                date = GetDateFromLine(PDFLines[indexOfIssueDate + 1]);

            Console.WriteLine("Data wystawienia:" + date);
        }

        public static void FindDateOfPayment(string[] PDFLines)
        {
            int indexOfDateOfPayment = PDFLines.Select((value, index) => new { Value = value, Index = index })
                      .Where(x => x.Value.Contains("termin", StringComparison.OrdinalIgnoreCase))
                      .Select(x => x.Index)
                      .First();

            string date = GetDateFromLine(PDFLines[indexOfDateOfPayment]);
            if (date.Equals("--"))
                date = GetDateFromLine(PDFLines[indexOfDateOfPayment + 1]);
            if (date.Equals("--"))
                date = GetDateFromLine(PDFLines[indexOfDateOfPayment - 1]);
            Console.WriteLine("Termin płatności:" + date);
        }

        public static void FindInvoiceNumber(string[] PDFLines)
        {
            int indexOfInvoiceNumber = PDFLines.Select((value, index) => new { Value = value, Index = index })
                                  .Where(x => x.Value.Contains("nr", StringComparison.OrdinalIgnoreCase))
                                  .Select(x => x.Index)
                                  .First();
            string line = PDFLines[indexOfInvoiceNumber].Substring(PDFLines[indexOfInvoiceNumber].IndexOf("NR", StringComparison.CurrentCultureIgnoreCase) + 3);

            Console.WriteLine("Nr faktury:" + line);
        }

        public static void FindDeliveryDate(string[] PDFLines)
        {
            int indexOfDeliveryDate = Array.FindIndex(PDFLines, line => line.Contains("data dostawy", StringComparison.OrdinalIgnoreCase));
            if (indexOfDeliveryDate < 0)
                indexOfDeliveryDate = Array.FindIndex(PDFLines, line => line.Contains("data wykonania", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine("Data dostawy: " + GetDateFromLine(PDFLines[indexOfDeliveryDate]));
        }

        public static void FindNIPNumber(string[] PDFLines)
        {
            int indexOfBuyer = Array.FindIndex(PDFLines, line => line.Contains("nabywca", StringComparison.OrdinalIgnoreCase));
            int indexOfSeller = Array.FindIndex(PDFLines, line => line.Contains("sprzedawca", StringComparison.OrdinalIgnoreCase));

            int[] indexesWithNIP = PDFLines.Select((value, index) => new { Value = value, Index = index })
                                  .Where(x => x.Value.Contains("NIP"))
                                  .Select(x => x.Index)
                                  .ToArray();
            int counter = 0;
            foreach (int n in indexesWithNIP)
            {
                if (n > indexOfBuyer || n > indexOfSeller)
                {
                    indexesWithNIP[counter++] = n;
                }
            }
            string sellerNIP, buyerNIP;


            if (indexOfBuyer < indexOfSeller)
            {
                buyerNIP = GetNIPNumberFromLine(PDFLines[indexesWithNIP[0]]);
                sellerNIP = GetNIPNumberFromLine(PDFLines[indexesWithNIP[1]]);
            }
            else
            {
                buyerNIP = GetNIPNumberFromLine(PDFLines[indexesWithNIP[1]]);
                sellerNIP = GetNIPNumberFromLine(PDFLines[indexesWithNIP[0]]);
            }
            Console.WriteLine("NIP nabywcy:" + buyerNIP);
            Console.WriteLine("NIP sprzedawcy:" + sellerNIP);
        }

        public static string GetDateFromLine(string line)
        {
            string date = string.Empty, day, month, year;

            string pattern1 = @"\d\d[-|.|\/]\d\d[-|.|\/]\d\d\d\d";
            Regex rgx1 = new Regex(pattern1);
            Match match1 = rgx1.Match(line);

            string pattern3 = @"\d\d\d\d[-|.|\/]\d\d[-|.|\/]\d\d";
            Regex rgx3 = new Regex(pattern3);
            Match match3 = rgx3.Match(line);


            if (match1.Success)
            {
                date = line.Substring(match1.Index, 10);
                day = date.Substring(0, 2);
                month = date.Substring(3, 2);
                year = date.Substring(6, 4);
            }
            else if (match3.Success)
            {
                date = line.Substring(match3.Index, 10);
                day = date.Substring(8, 2);
                month = date.Substring(5, 2);
                year = date.Substring(0, 4);
            }
            else
            {
                string[] monthNames = {"NO OCCURANCES", "styczeń", "luty", "marzec", "kwiecień", "maj",
                    "czerwiec", "lipiec", "sierpień", "wrzesień", "październik", "listopad", "grudzień" };
                int indexOfMonthOccurancy = monthNames.Select((value, index) => new { Value = value, Index = index })
                                  .Where(x => line.Contains(x.Value, StringComparison.OrdinalIgnoreCase))
                                  .Select(x => x.Index)
                                  .FirstOrDefault();

                if (indexOfMonthOccurancy == 0)
                {
                    day = "";
                    month = "";
                    year = "";
                }
                else
                {
                    date = line.Substring(line.IndexOf(monthNames[indexOfMonthOccurancy]) - 3, monthNames[indexOfMonthOccurancy].Length + 9);
                    day = date.Substring(0, 2);
                    if (indexOfMonthOccurancy < 10)
                        month = "0" + (indexOfMonthOccurancy + 1).ToString();
                    else month = (indexOfMonthOccurancy + 1).ToString();
                    year = date.Substring(monthNames[indexOfMonthOccurancy].Length + 5, 4);
                }
            }
            return day + "-" + month + "-" + year;
        }
        public static string GetNIPNumberFromLine(string line)
        {
            string NIPNumber = string.Empty;
            line = line.Substring(line.IndexOf("NIP"));
            for (int i = 0; i < line.Length; i++)
            {
                if (Char.IsDigit(line[i]))
                    NIPNumber += line[i];
            }

            return NIPNumber.Substring(0, 10);
        }
    }
}
