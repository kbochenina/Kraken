using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.IO;

namespace MITP
{
    public partial class ExecutionBrokerService
    {
        public virtual bool MagicHappens()
        {
            return false;

            string id = IOProxy.Storage.Upload(@"D:/Projects/_TestSuite/Molec/H2O.xyz", "/tests/execution/H2O.xyz");

            id = IOProxy.Storage.Upload(@"ftp://192.168.4.1/ex_test/120.txt", "/tests/execution/120.txt");
            var o1 = IOProxy.Storage.GetFileSize(id);
            string tmpFile = @"ftp://192.168.4.1/ex_test/1/2/3/4/120.out.txt"; //Path.GetTempFileName();
            IOProxy.Storage.Download(id, tmpFile);
            //var o2 = File.ReadAllText(tmpFile);
            //File.Delete(tmpFile);


            id = IOProxy.Storage.Upload(@"ftp://192.168.4.1/ex_test/8.txt", "/tests/execution/8.txt");
            o1 = IOProxy.Storage.GetFileSize(id);
            tmpFile = @"ftp://192.168.4.1/ex_test/1/8.out.txt"; //Path.GetTempFileName();
            IOProxy.Storage.Download(id, tmpFile);
            //o2 = File.ReadAllText(tmpFile);
            //File.Delete(tmpFile);
            return true;

            return false;

            //string id = IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\TestP\12.txt", "/tests/execution/12.txt");
            //id = IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\TestP\86.txt", "/tests/execution/86.txt");

            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\CNM\cnm_60k_p32", "/tests/execution/cnm_60k_p32");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\CNM\mmCnmTest.in", "/tests/execution/cnm_20k_p8");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\CNM\mmCnmTest_200k.in", "/tests/execution/cnm_200k_p8");

            return true;

            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\Molec\H2O_head.xyz", "/tests/execution/H2O");


            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\SWAN\2008_11_15-16_hirlam.zip", "/tests/execution/hirlam");
            return true;


            string[] filePaths = System.IO.Directory.GetFiles(@"D:\Temp\Nums\");
            foreach (string filePath in filePaths)
            {
                string fileName = System.IO.Path.GetFileName(filePath);
                IOProxy.Storage.Upload(filePath, "/tests/execution/" + fileName);
            }
            return true;

            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\Molec\H2O_head.xyz", "/tests/execution/H2O");
            return true;

            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\CNM\input.txt", "/tests/execution/cnm");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\ISM\configuration.conf", "/tests/execution/ism_config");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\ISM\graph.sp", "/tests/execution/ism_graph");
            return true;

            for (int i = 0; i < 256; i++)
            {
                string num = i.ToString();
                byte[] bytes = Encoding.UTF8.GetBytes(num);
                byte[] bytesWithSig = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(num)).ToArray();

                System.IO.File.WriteAllBytes(@"D:\Temp\Nums\number" + num, bytes);
                //System.IO.File.WriteAllBytes(@"D:\Temp\Nums\number" + num + "bom", bytesWithSig);
            }

            //var res = Resource.Load(new string[] { @"d:\temp\resource_base.js" });
            //Log.Debug(res.First().ResourceName);

            return true;

            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\JAggregate\JAggregate.dataList", "JAggregateDataList");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Done\JAggregate\JAggregateInitialBeam.dataList", "JAggregateInitialBeam");
            return true;


            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Pim\Nanoflow\IO_Lab\inInfo1.txt", "/tests/execution/nanoflowInInfo");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Pim\Nanoflow\IO_Lab\inInfo1.txt", "/tests/execution/nanoflowInInfo1");
            IOProxy.Storage.Upload(@"D:\Projects\_TestSuite\_Pim\Nanoflow\IO_Lab\strTube.txt", "/tests/execution/nanoflowStrTube");
            return true;

            IOProxy.Storage.Upload(@"D:\Temp\SempTestInput", "/tests/execution/SempTestInput");
            return true;


            //string storageId = IOProxy.Storage.GetNewInputId();
            //string retId = IOProxy.Storage.MoveInsideLocalFile(@"D:\Nano\Molec\H2O.xyz", storageId);
            //retId = IOProxy.Storage.MoveInsideLocalFile(@"D:\Nano\Molec\H2O.xyz", "H2O");
            return true;
        }

    }
}
