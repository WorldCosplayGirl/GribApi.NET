﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Grib.Api.Interop;
using System.Text.RegularExpressions;
using System.Threading;

namespace Grib.Api.Tests
{
    [TestFixture]
    public class Get
    {
        [Test]
        public void TestGetCounts ()
        {
            using (GribFile file = new GribFile(Settings.GRIB))
            {
                Assert.IsTrue(file.MessageCount > 0);
                foreach (var msg in file)
                {
                    Assert.AreNotEqual(msg.DataPointsCount, 0);
                    Assert.AreNotEqual(msg.ValuesCount, 0);
                    Assert.AreEqual(msg.ValuesCount, msg["numberOfCodedValues"].AsInt());
                    Assert.IsTrue(msg["numberOfCodedValues"].IsReadOnly);
                    Assert.AreEqual(msg.DataPointsCount, msg.ValuesCount + msg.MissingCount);
                }
            }
        }

        [Test]
        public void TestGetVersion ()
        {
            Regex re = new Regex(@"^(\d+\.)?(\d+\.)?(\*|\d+)$");
            Assert.IsTrue(re.IsMatch(GribEnvironment.GribApiVersion));
        }

        [Test]
        public void TestGetNativeType ()
        {
            using (GribFile file = new GribFile(Settings.REG_LATLON_GRB1))
            {
                var msg = file.First();
                Assert.AreEqual(msg["packingType"].NativeType, GribValueType.String);
                Assert.AreEqual(msg["longitudeOfFirstGridPointInDegrees"].NativeType, GribValueType.Double);
                Assert.AreEqual(msg["numberOfPointsAlongAParallel"].NativeType, GribValueType.Int);
                Assert.AreEqual(msg["values"].NativeType, GribValueType.DoubleArray);

                // TODO: test other types
            }
        }

        [Test]
        public void TestCanConvertToDegress ()
        {
            using (GribFile file = new GribFile(Settings.REDUCED_LATLON_GRB2))
            {
                var msg = file.First();

                // true
                Assert.IsTrue(msg["latitudeOfFirstGridPointInDegrees"].CanConvertToDegrees);
                Assert.IsTrue(msg["latitudeOfFirstGridPoint"].CanConvertToDegrees);
                Assert.IsTrue(msg["longitudeOfFirstGridPointInDegrees"].CanConvertToDegrees);
                Assert.IsTrue(msg["longitudeOfFirstGridPoint"].CanConvertToDegrees);
                Assert.IsTrue(msg["latitudeOfLastGridPointInDegrees"].CanConvertToDegrees);
                Assert.IsTrue(msg["latitudeOfLastGridPoint"].CanConvertToDegrees);
                Assert.IsTrue(msg["jDirectionIncrement"].CanConvertToDegrees);
                Assert.IsTrue(msg["iDirectionIncrement"].CanConvertToDegrees);

                // false
                Assert.IsFalse(msg["numberOfPointsAlongAParallel"].CanConvertToDegrees);
                Assert.IsFalse(msg["numberOfPointsAlongAParallelInDegrees"].CanConvertToDegrees);
                Assert.IsFalse(msg["numberOfPointsAlongAMeridian"].CanConvertToDegrees);
                Assert.IsFalse(msg["numberOfPointsAlongAMeridianInDegrees"].CanConvertToDegrees);
                Assert.IsFalse(msg["packingType"].CanConvertToDegrees);
            }
        }

        [Test]
        public void TestGetGrib2 ()
        {
            double delta = 0.1d;

            using (GribFile file = new GribFile(Settings.REDUCED_LATLON_GRB2))
            {
                var msg = file.First();

                // "InDegrees" is a magic token that converts coordinate double values to degrees
                // explicit degree conversion via key name
                double latitudeOfFirstGridPointInDegrees = msg["latitudeOfFirstGridPoint"].AsDouble();
                Assert.AreEqual(latitudeOfFirstGridPointInDegrees, 90, delta);

                // degree conversion via accessor
                double longitudeOfFirstGridPointInDegrees = msg["longitudeOfFirstGridPoint"].AsDouble(/* inDegrees == true */);
                Assert.AreEqual(longitudeOfFirstGridPointInDegrees, 0, delta);

                // degree conversion via accessor
                double latitudeOfLastGridPointInDegrees = msg["latitudeOfLastGridPoint"].AsDouble(/* inDegrees == true */);
                Assert.AreEqual(latitudeOfLastGridPointInDegrees, -90, delta);

                // degree conversion via accessor
                double longitudeOfLastGridPointInDegrees = msg["longitudeOfLastGridPoint"].AsDouble(/* inDegrees == true */);
                Assert.AreEqual(longitudeOfLastGridPointInDegrees, 360, .5);

                // degree conversion via accessor
                double jDirectionIncrementInDegrees = msg["jDirectionIncrement"].AsDouble(/* inDegrees == true */);
                Assert.AreEqual(jDirectionIncrementInDegrees, 0.36, delta);

                // degree conversion via accessor
                double iDirectionIncrementInDegrees = msg["iDirectionIncrement"].AsDouble(/* inDegrees == true */);
                Assert.AreEqual(iDirectionIncrementInDegrees, -1.0E+100, delta);

                int numberOfPointsAlongAParallel = msg["numberOfPointsAlongAParallel"].AsInt();
                Assert.AreEqual(numberOfPointsAlongAParallel, -1);

                int numberOfPointsAlongAMeridian = msg["numberOfPointsAlongAMeridian"].AsInt();
                Assert.AreEqual(numberOfPointsAlongAMeridian, 501);

                string packingType = msg["packingType"].AsString();
                Assert.AreEqual("grid_simple", packingType);
            }
        }

        [Test]
        public void TestGetParallel ()
        {
            var files = new[] { Settings.REDUCED_LATLON_GRB2, Settings.BIN, Settings.COMPLEX_GRID, Settings.REG_LATLON_GRB1, Settings.GAUSS, Settings.PACIFIC_WIND };

            Parallel.ForEach(files, (path, s) =>
            {
                using (var file = new GribFile(path))
                {
                    Parallel.ForEach(file, (msg, s2) =>
                    {
                        if (msg.ShortName == "shww") return;

                        try
                        {
                            foreach (var v in msg.GeoSpatialValues)
                            {
                                Assert.AreNotEqual(Double.NaN, v.Latitude);
                                Assert.AreNotEqual(Double.NaN, v.Longitude);
                                Assert.AreNotEqual(Double.NaN, v.Value);
                            }
                        } catch (GribApiException e)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(msg.ShortName);
                            Console.WriteLine(msg.ToString());
                            Assert.IsTrue(false);
                        }
                    });
                }

            });
        }

		[TestFixture]
		public class IterateValues
		{
			[Test]
			public void TestIterateKeyValuePairs()
			{
				using (var file = new GribFile(Settings.BIN)) {
					Assert.IsTrue(file.MessageCount > 0);
					Assert.IsTrue(file.First().Any());
				}
			}

			[Test]
			public void TestIterateLatLong()
			{
				Console.WriteLine();

				using (var file = new GribFile(Settings.REDUCED_LATLON_GRB2)) {
					Assert.IsTrue(file.MessageCount > 0);
					foreach (var msg in file) {
						int c = msg.Count();

						msg.Namespace = "geography";

						Assert.AreNotEqual(c, msg.Count());

						Assert.IsTrue(msg.GeoSpatialValues.Any());
					}
				}
			}
		}

		[Test]
		public void TestTime()
		{
			using (GribFile file = new GribFile(Settings.TIME))
			{
				Assert.IsTrue(file.MessageCount > 0);

				int diff = 0;
				int i = 0;

				foreach (var msg in file) 
				{
					if (i++ == 0) { continue; }

					diff = msg["P2"].AsInt();
					Assert.IsTrue(diff > 0);
					var t = msg.ReferenceTime.AddHours(diff);
					Assert.AreNotEqual(msg.ReferenceTime, t);
					Assert.AreEqual(t, msg.Time);
				}
				Assert.IsTrue(i > 2);
			}
		}
    }
}
