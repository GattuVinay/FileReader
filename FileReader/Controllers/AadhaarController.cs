using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using Tesseract;
using FileReader.Models;
using PdfiumViewer;
using SkiaSharp;
using System.Text.RegularExpressions;
using System.Text;

namespace FileReader.Controllers
{
    public class AadhaarController : Controller
    {
        // GET: Upload Aadhaar
        public ActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file, IFormFile file1, IFormFile file2)
        {
            try
            {
                // Handle single or multiple file uploads
                var filesToProcess = new List<IFormFile>();
                string extractedText = string.Empty;
                // Add files based on the upload method (single/multiple)
                if (file != null && file.Length > 0)
                {
                    filesToProcess.Add(file);
                }
                if (file1 != null && file1.Length > 0)
                {
                    filesToProcess.Add(file1);
                }
                if (file2 != null && file2.Length > 0)
                {
                    filesToProcess.Add(file2);
                }

                // Process each uploaded file
                foreach (var uploadedFile in filesToProcess)
                {
                    var fileName = Path.GetFileName(uploadedFile.FileName);
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");

                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var filePath = Path.Combine(uploadsFolder, fileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        uploadedFile.CopyTo(stream);
                    }


                    if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        // Handle PDF files
                        extractedText += ExtractTextFromPdf(filePath);
                    }
                    else
                    {
                        // Handle image files
                        extractedText += ExtractTextFromImage(filePath);
                    }
                }

                // Parse Aadhaar details from the extracted text
                var person = ParseAadhaarDetails(extractedText);
                return RedirectToAction("PersonDetails", person);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Invalid file format. Please upload a valid Image or PDF file.";
                return View("UploadWithError");
            }
        }



        // Extract text from PDF using PdfiumRenderer and apply OCR on each page
        private string ExtractTextFromPdf(string pdfPath)
        {
            string extractedText = string.Empty;

            using (var pdfDocument = PdfDocument.Load(pdfPath))
            {
                for (int i = 0; i < pdfDocument.PageCount; i++)
                {
                    using (var page = pdfDocument.Render(i, 300, 300, true))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            page.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png); 
                            var img = Pix.LoadFromMemory(memoryStream.ToArray());

                            // Perform OCR on the image
                            extractedText += ExtractTextFromImage(img); 
                        }
                    }
                }
            }

            return extractedText;
        }

        // Extract text from image files using Tesseract OCR
        private string ExtractTextFromImage(string imagePath)
        {
            using (var img = Pix.LoadFromFile(imagePath))
            {
                return ExtractTextFromImage(img);
            }
        }

        // Common method to extract text from Pix object (used for both images and PDF pages)
        private string ExtractTextFromImage(Pix img)
        {
            string extractedText = string.Empty;
            var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TessData");

            // Preprocess image (optional)
            var processedImg = PreprocessImage(img);

            // Using Tesseract for OCR
            using (var ocrEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default))
            {
                using (var page = ocrEngine.Process(processedImg))
                {
                    extractedText = page.GetText();
                }
            }

            return extractedText;
        }

        // Preprocess image (optional step to improve OCR accuracy)
        private Pix PreprocessImage(Pix img)
        {
            var grayImage = img.ConvertRGBToGray();
            // Additional preprocessing steps like contrast adjustment or sharpening can be added here
            return grayImage;
        }

        // Parse Aadhaar details from the extracted text
        private AdharModel ParseAadhaarDetails(string extractedText)
        {
            var adhar = new AdharModel();
            try
            {
                // Split the extracted text into lines
                var lines = extractedText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                // Iterate through each line and extract relevant details
                for (int i = 0; i < lines.Length; i++)
                {
                    var trimmedLine = lines[i].Trim();

                    // Extract DOB (Look for "DOB", "008 :", "Year of Birth", or its regional variants)
                    if (trimmedLine.Contains("DOB") || trimmedLine.Contains("008 :")|| trimmedLine.Contains("Year of Birth") || trimmedLine.Contains("0౦8"))
                    {
                        adhar.DOB = ExtractDOB(trimmedLine);

                        // Extract name from the line before DOB
                        if (i > 0)
                        {
                            adhar.FirstName = lines[i - 1].Trim();
                        }
                        else
                        {
                            adhar.FirstName = "Name Not Found";
                        }

                        // Handle gender based on extracted information
                        if (adhar.Gender == "Female")
                        {
                            adhar.FirstName = lines[i - 4].Trim();
                        }
                        else
                        {
                            adhar.FirstName = lines[i - 1].Trim();
                        }
                    }

                    else if (trimmedLine.Contains("Male") || trimmedLine.Contains("Female"))
                    {
                        adhar.Gender = ExtractGender(trimmedLine);
                    }

                    else if (trimmedLine.Contains("S/O") || trimmedLine.Contains("D/O") || trimmedLine.Contains("5/0") || trimmedLine.Contains("Father"))
                    {
                        adhar.Father = ExtractFather(trimmedLine);
                    }

                    else if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"\d{4}\s\d{4}\s\d{4}"))
                    {
                        adhar.Adharnumber = GetAadhaarNumber(trimmedLine);
                    }

                    // Extract Address from text after finding "Address"
                    if (trimmedLine.Contains("Address"))
                    {
                        adhar.Address = ExtractAddress(lines.Skip(i).ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return adhar;
        }

        // Utility function to extract DOB from a line
        private string ExtractDOB(string line)
        {
            var match = Regex.Match(line, @"\d{2}/\d{2}/\d{4}");
            return match.Success ? match.Value : string.Empty;
        }

        // Utility function to extract gender from a line
        private string ExtractGender(string line)
            {
                if (line.Contains("Male"))
            {
                return "Male";
            }
            else if (line.Contains("Female"))
            {
                return "Female";
            }

            return string.Empty;
        }

        //  function to extract Father's name
        private string ExtractFather(string line)
        {
            var match = Regex.Match(line, @"(S/O|D/O|Father)\s+(.+?),");
            return match.Success ? match.Groups[2].Value : string.Empty;
        }

        //  function to extract address by concatenating lines until a 6-digit postal code is found
        private string ExtractAddress(string[] lines)
        {
            StringBuilder addressBuilder = new StringBuilder();
            bool addressStarted = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (trimmedLine.Contains("Address"))
                {
                    addressStarted = true;
                }

                if (addressStarted)
                {
                    // Look for the first comma and split the string at the comma
                    if (trimmedLine.Contains(","))
                    {
                        // Split only on the first comma and take the part after it
                        var parts = trimmedLine.Split(new[] { ',' }, 2);
                        if (parts.Length > 1)
                        {
                           
                            addressBuilder.Append(parts[1].Trim()).Append(" ");
                        }
                    }
                    else
                    {
                        addressBuilder.Append(trimmedLine).Append(" ");
                    }
                }
                if (Regex.IsMatch(trimmedLine, @"\d{6}$"))
                {
                    break;
                }
            }

            return addressBuilder.ToString().Trim(); 

        }

        // Utility function to extract Aadhaar number (format: 1234-5678-9012)
        private string GetAadhaarNumber(string extractedText)
        {
            var regex = new Regex(@"\b\d{4}\s\d{4}\s\d{4}\b");
            var match = regex.Match(extractedText);
            return match.Success ? match.Value.Replace(" ", "-") : string.Empty;
        }

        // Action to display Aadhaar details in the view
        public ActionResult PersonDetails(AdharModel adhar)
        {
            return View(adhar);
        }
    }
}
