// Extract Texture2D XNB assets to PNG using FNA.
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

static class ExtractTexture
{
    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Usage: ExtractTexture <contentDir> <assetName> <outPng>");
            return 1;
        }

        string contentDir = Path.GetFullPath(args[0]);
        string assetName = args[1];
        string outPng = Path.GetFullPath(args[2]);
        string outDir = Path.GetDirectoryName(outPng);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        using (ExtractGame game = new ExtractGame(contentDir))
        {
            Texture2D texture = game.Content.Load<Texture2D>(assetName);
            using (FileStream fs = File.OpenWrite(outPng))
            {
                texture.SaveAsPng(fs, texture.Width, texture.Height);
            }
            Console.WriteLine("Wrote " + outPng + " (" + texture.Width + "x" + texture.Height + ")");
        }
        return 0;
    }
}

class ExtractGame : Microsoft.Xna.Framework.Game
{
    public ExtractGame(string contentDir)
    {
        GraphicsDeviceManager gdm = new GraphicsDeviceManager(this)
        {
            GraphicsProfile = GraphicsProfile.Reach,
        };
        Services.AddService(typeof(GraphicsDevice), gdm.GraphicsDevice);
        Content = new ContentManager(Services, contentDir);
    }
}
