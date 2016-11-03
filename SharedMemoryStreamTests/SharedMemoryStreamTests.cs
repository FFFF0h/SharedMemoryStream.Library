// SharedMemoryStream (File: SharedMemoryStreamTests\SharedMemoryStreamTests.cs)
// Copyright (c) 2016 Laurent Le Guillermic
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO.SharedMemory;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace SharedMemoryStreamTests
{
    [TestClass]
    public class SharedMemoryStreamTests
    {
        #region Spin tests
        [TestMethod]
        public void Spin_Locked()
        {

            string spinName = "test";
            if (DynamicSpin.Acquire(spinName))
            {
                if (DynamicSpin.Acquire(spinName))
                {
                    Assert.Fail();
                }
                DynamicSpin.Release(spinName);
            }
            else
            {
                Assert.Fail();
            }
        }
        #endregion

        #region Stream tests

        [TestMethod]
        public void Spin_Simple()
        {
            string spinName = "test";
            if (DynamicSpin.Acquire(spinName))
            {

                DynamicSpin.Release(spinName);
            }
            else
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void Stream_Constructor()
        {
            string name = Guid.NewGuid().ToString();
            using (SharedMemoryStream producer = new SharedMemoryStream(name))
            using (SharedMemoryStream consumer = new SharedMemoryStream(name))
            {

            }
        }

        [TestMethod]
        public void Stream_Simple_ReadWrite()
        {
            string name = Guid.NewGuid().ToString();
            string expected = "This is a test !";

            using (SharedMemoryStream buffer = new SharedMemoryStream(name))
            using (StreamWriter writer = new StreamWriter(buffer))
            using (StreamReader reader = new StreamReader(buffer))
            {
                writer.WriteLine(expected);
                writer.Flush();
                string readed = reader.ReadLine();
                Assert.AreEqual(expected, readed, false);
            }
        }

        [TestMethod]
        public void Stream_Simple_BigWrite_ReadWrite()
        {
            string name = Guid.NewGuid().ToString();

            Random r = new Random();
            int bufSize = 32;
            byte[] data = new byte[bufSize * 2 + 10];
            byte[] readBuf = new byte[bufSize * 2 + 10];

            r.NextBytes(data);

            using (SharedMemoryStream buffer = new SharedMemoryStream(name, 512, bufSize))
            using (BinaryWriter writer = new BinaryWriter(buffer))
            using (BinaryReader reader = new BinaryReader(buffer))
            {
                writer.Write(data, 0, data.Length);
                writer.Flush();
                //reader.Read(readBuf, 0, readBuf.Length);

                writer.Write(data, 0, data.Length);
                writer.Flush();
                reader.Read(readBuf, 0, readBuf.Length);

                writer.Write(data, 0, data.Length);
                writer.Flush();
                reader.Read(readBuf, 0, readBuf.Length);

                for (var i = 0; i < data.Length; i++)
                    Assert.AreEqual(data[i], readBuf[i], String.Format("Data written does not match data read at index {0}", i));
            }
        }


        [TestMethod]
        public void Stream_Object_ReadWrite()
        {
            string name = Guid.NewGuid().ToString();
            DateTime expected = DateTime.Now;

            using (SharedMemoryStream buffer = new SharedMemoryStream(name))
            using (SharedMemoryStreamWriter<DateTime> writer = new SharedMemoryStreamWriter<DateTime>(buffer))
            using (SharedMemoryStreamReader<DateTime> reader = new SharedMemoryStreamReader<DateTime>(buffer))
            {
                writer.WriteObject(expected);
                writer.Flush();
                DateTime red = reader.ReadObject();
                Assert.AreEqual(expected, red);
            }
        }

   
        private void Stream_Parallel_Types_ReadWrite<T>(T data)
        {
            int iterations = 10;
            string name = Guid.NewGuid().ToString();

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                using (SharedMemoryStream buffer = new SharedMemoryStream(name))
                using (SharedMemoryStreamWriter<T> writer = new SharedMemoryStreamWriter<T>(buffer))
                using (SharedMemoryStreamReader<T> reader = new SharedMemoryStreamReader<T>(buffer))
                {
                    Action wt = () =>
                    {
                        writer.WriteObject(data);
                        Debug.WriteLine("Write done.", "Information");
                    };

                    Action rd = () =>
                    {
                        reader.ReadObject();
                        Debug.WriteLine("Read done.", "Information");
                    };

                    Task tWriter = Task.Factory.StartNew(wt);
                    Task tReader = Task.Factory.StartNew(rd);

                    if (!Task.WaitAll(new Task[] { tWriter, tReader }, 60000))
                    {
                        Assert.Fail("Reader or writer took too long");
                    }
                }
            }
            sw.Stop();
            double time = Math.Round(sw.ElapsedMilliseconds / (double)iterations, 2);

            Console.WriteLine("Time: " + time + "ms, Type: " + typeof(T).FullName);
        }

        [TestMethod]
        public void Stream_Parallel_Types_ReadWrite()
        {
            int size = 10000;

            // Bytes
            byte[] data = new byte[size];
            Random r = new Random();
            r.NextBytes(data);
            Debug.WriteLine("Pushing byte array...", "Information");
            Stream_Parallel_Types_ReadWrite(data);
            Stream_Parallel_Types_ReadWrite((object)data);

            // String
            string s = new string('*', size);
            Debug.WriteLine("Pushing string...", "Information");
            Stream_Parallel_Types_ReadWrite(s);

            // DateTime
            DateTime t = DateTime.Now;
            Debug.WriteLine("Pushing DateTime object...", "Information");
            Stream_Parallel_Types_ReadWrite(t);
        }


        private void Stream_Parallel_ReadWrite(int dataSize, int nodeCount, int bufferSize, bool bench)
        {
            int iterations = 10;

            byte[] data = new byte[dataSize];
            string name = Guid.NewGuid().ToString();

            // Fill with random data
            Random r = new Random();
            r.NextBytes(data);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                using (SharedMemoryStream buffer = new SharedMemoryStream(name, nodeCount, bufferSize))
                using (SharedMemoryStreamWriter<byte[]> writer = new SharedMemoryStreamWriter<byte[]>(buffer))
                using (SharedMemoryStreamReader<byte[]> reader = new SharedMemoryStreamReader<byte[]>(buffer))
                {
                    Action wt = () =>
                    {
                        writer.WriteObject(data);
                        Debug.WriteLine("Write done.", "Information");
                    };

                    Action rd = () =>
                    {
                        reader.ReadObject();
                        Debug.WriteLine("Read done.", "Information");
                    };

                    Task tWriter = Task.Factory.StartNew(wt);
                    Task tReader = Task.Factory.StartNew(rd);

                    if (!Task.WaitAll(new Task[] { tWriter, tReader }, 1200000))
                    {
                        Assert.Fail("Reader or writer took too long");
                    }
                }
            }
            sw.Stop();
            double dataRate = Math.Round(((double)iterations * dataSize) / sw.ElapsedMilliseconds, 2);

            if (bench)
                Console.WriteLine(dataSize + ";" + dataRate + ";" + sw.ElapsedMilliseconds + ";" + nodeCount + ";" + bufferSize);
            else
                Console.WriteLine("Data Rate: " + dataRate + "kB/s (" + sw.ElapsedMilliseconds + "ms to write " + iterations + "x" + dataSize + "=" + iterations * dataSize + " bytes in " + nodeCount + "x" + bufferSize + "=" + nodeCount * bufferSize + ")");  
        }

        private void Stream_Parallel_ReadWrite(int dataSize)
        {
            bool bench = false;
            Stream_Parallel_ReadWrite(dataSize, 3, 32, bench);
            Stream_Parallel_ReadWrite(dataSize, 3, 1024, bench);
            Stream_Parallel_ReadWrite(dataSize, 3, 4096, bench);
            Stream_Parallel_ReadWrite(dataSize, 3, 8192, bench);

            Stream_Parallel_ReadWrite(dataSize, 3, 1000000, bench);
            Stream_Parallel_ReadWrite(dataSize, 32768, 32, bench);

            Stream_Parallel_ReadWrite(dataSize, 512, 32, bench);
            Stream_Parallel_ReadWrite(dataSize, 32, 512, bench);
            Stream_Parallel_ReadWrite(dataSize, 1024, 32, bench);
            Stream_Parallel_ReadWrite(dataSize, 32, 1024, bench);
            Stream_Parallel_ReadWrite(dataSize, 4096, 32, bench);
            Stream_Parallel_ReadWrite(dataSize, 32, 4096, bench);
            Stream_Parallel_ReadWrite(dataSize, 8192, 32, bench);
            Stream_Parallel_ReadWrite(dataSize, 32, 8192, bench);

            Stream_Parallel_ReadWrite(dataSize, 512, 512, bench);
            Stream_Parallel_ReadWrite(dataSize, 1024, 1024, bench);

            Stream_Parallel_ReadWrite(dataSize, 1024, 8192, bench);
            Stream_Parallel_ReadWrite(dataSize, 8192, 4096, bench);

            Stream_Parallel_ReadWrite(dataSize, 4096, 1024, bench);
            Stream_Parallel_ReadWrite(dataSize, 1024, 4096, bench); 
        }

        [TestMethod]
        public void Stream_Parallel_Bench_ReadWrite()
        {
            Stream_Parallel_ReadWrite(100);
            Stream_Parallel_ReadWrite(1000);
            Stream_Parallel_ReadWrite(10000);
            Stream_Parallel_ReadWrite(100000);
            Stream_Parallel_ReadWrite(1000000);
            //Stream_Parallel_ReadWrite(10000000);
        }

        [TestMethod]
        public void Stream_ConsumerProcuder_ReadWrite()
        {
            string name = Guid.NewGuid().ToString();
            using (SharedMemoryStream producer = new SharedMemoryStream(name))
            using (StreamWriter writer = new StreamWriter(producer))
            using (SharedMemoryStream consumer = new SharedMemoryStream(name))
            using (StreamReader reader = new StreamReader(consumer))
            {
                string expected = "This is a test !";
                writer.WriteLine(expected);
                writer.Flush();
                string readed = reader.ReadLine();
                Assert.AreEqual(expected, readed, false);
            }
        }

        #endregion
    }
}
