using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace MITP
{
	public partial class OrcaAdapter : CommonXyzPackageAdapter
	{
		public const double E = 2.5;

		public double[] GetIntervals()
		{
			double L = 0.0;
			double R = 4000.0;
			double intLen = 100.0;
			int intsCount = (int) Math.Ceiling((R - L) / intLen) + 1; 
			double[] ints = new double[intsCount];

			double val = L;
			for (int i = 0; i < intsCount; i++)
			{
				ints[i] = val;
				val += intLen;
			}

			return ints;
		}

		public int[] CalcFreqs(double[] intervals, double[] values)
		{
			int len = intervals.Length;
			int[] freqs = new int[len-1];
			int[] freqsBorders = new int[len-1];

			foreach (double value in values)
			{
				for (int i = 0; i < len-1; i++)
				{
					if (intervals[i] + E <= value && value < intervals[i + 1] - E)
						freqs[i]++;

					if (intervals[i] - E <= value && value < intervals[i] + E)
						freqsBorders[i]++;
				}
			}

			for (int i = 0; i < len - 1; i++)
				freqs[i] *= 2;

			for (int i = 0; i < len - 2; i++)
			{
				freqs[i] += freqsBorders[i + 1];
				freqs[i + 1] += freqsBorders[i + 1];
			}

			return freqs;
		}

		public double[] ExtractValues(string fileContent)
		{
			string patternCutSection =
				@"(?<=VIBRATIONAL FREQUENCIES[^\d]*)"
				+ @"\d(?>[\s\S]+?--)"
			;

			var regexCutSection = new Regex(patternCutSection);
			string section = regexCutSection.Match(fileContent).Value;

			string patternCutNums = 
				@"(?<=\d*:\s*)"
				+ @"[\d\.]+"
			;

			var regexCutNums = new Regex(patternCutNums);
			var matches = regexCutNums.Matches(section);

			double[] values = new double[matches.Count];
			for (int i=0; i<matches.Count; i++)
				values[i] = Double.Parse(matches[i].Value.Replace(".", ","));


			return values;
		}


        public override void OnFinish(Task task, string ftpFolder)
		{
			const int MAX_ATTEMPTS = 3;

			foreach (string paramName in task.Params.Keys)
			{
				if (paramName.ToLower() == "osc_freq")
				{
					bool succeeded = false;
					Exception lastException = null;
					for (int attemptNum = 0; !succeeded && attemptNum < MAX_ATTEMPTS; attemptNum++)
					{
						try
						{
							var singleOutputFile = task.OutputFiles.Last(file => file.FileName.EndsWith(OUTPUT_FILE_EXT));

                            string fileName = Path.GetFileName(singleOutputFile.FileName);
                            string folderName = ftpFolder + Path.GetDirectoryName(singleOutputFile.FileName);
							if (!folderName.EndsWith("/"))
								folderName += "/";

							string content = IOProxy.Ftp.DownloadFileContent(folderName, fileName);

							double[] values = ExtractValues(content);
							double[] intervals = GetIntervals();
							int[] freqs = CalcFreqs(intervals, values);

							var writer = new StringWriter();
							writer.WriteLine("Density of phonon (vibration) states (doubled)");
							for (int i = 0; i < freqs.Length; i++)
								writer.WriteLine("{0,4}-{1,-4} {2,4} {3}", intervals[i], intervals[i + 1], (intervals[i] + intervals[i + 1]) / 2, freqs[i]);
							writer.Close();

							IOProxy.Ftp.UploadFileContent(writer.ToString(), folderName, "phonon.hist");

							succeeded = true;
						}
						catch (Exception e)
						{
							lastException = e;
						}
					}

					if (!succeeded)
						throw lastException;
				}
			}

			base.OnFinish(task, ftpFolder);
		}
	}
}
