using System;
using System.IO;
using NUnit.Framework;

namespace Glassfall.Tests
{
    public sealed class SupportingPixelArtAssetTests
    {
        private static readonly (string path, int width, int height)[] Assets =
        {
            ("Assets/Glassfall/Art/Sprites/Enemies/ArcHunter.png", 64, 64),
            ("Assets/Glassfall/Art/Sprites/Objectives/ResonanceNode.png", 64, 64),
            ("Assets/Glassfall/Art/Sprites/Environment/DirtyGlassPatch.png", 128, 128),
            ("Assets/Glassfall/Art/Sprites/Environment/GlassFloorTile.png", 128, 128),
        };

        [Test]
        public void SupportingPixelArtAssetsHaveExpectedDimensions()
        {
            foreach ((string path, int width, int height) in Assets)
            {
                byte[] bytes = File.ReadAllBytes(ToProjectPath(path));
                AssertPng(bytes, path);

                int actualWidth = ReadBigEndianInt32(bytes, 16);
                int actualHeight = ReadBigEndianInt32(bytes, 20);

                Assert.That(actualWidth, Is.EqualTo(width), path);
                Assert.That(actualHeight, Is.EqualTo(height), path);
            }
        }

        [Test]
        public void SupportingPixelArtAssetsAreRgbaPngs()
        {
            foreach (var asset in Assets)
            {
                string path = asset.path;
                byte[] bytes = File.ReadAllBytes(ToProjectPath(path));
                AssertPng(bytes, path);

                // PNG IHDR color type 6 = truecolor with alpha (RGBA).
                Assert.That(bytes[25], Is.EqualTo(6), path);
            }
        }

        private static string ToProjectPath(string relativePath)
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void AssertPng(byte[] bytes, string path)
        {
            Assert.That(bytes.Length, Is.GreaterThan(25), path);
            byte[] signature =
            {
                137, 80, 78, 71, 13, 10, 26, 10
            };

            for (int i = 0; i < signature.Length; i++)
                Assert.That(bytes[i], Is.EqualTo(signature[i]), path);
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return
                (bytes[offset] << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3];
        }
    }
}
