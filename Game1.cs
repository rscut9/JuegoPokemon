using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MiJuegoPokemon;

// --- ESTRUCTURA DE DATOS ---
enum GameState { Menu, Opciones, SeleccionPersonaje, Exploracion, Lista, Pokedex, Anadir }

public class TiledMap {
    public int width { get; set; }
    public int height { get; set; }
    public int tilewidth { get; set; }
    public int tileheight { get; set; }
    public List<TiledLayer> layers { get; set; }
}

public class TiledLayer {
    public List<int> data { get; set; }
    public string name { get; set; }
}

class Personaje {
    public string Nombre;
    public string Tipo;
    public string Descripcion;
    public Texture2D Textura;
}

// --- CLASE PRINCIPAL ---
public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private RenderTarget2D _renderTarget;
    private Rectangle _screenDestination;

    // Constantes de resolución virtual
    private const int VirtualWidth = 1600;
    private const int VirtualHeight = 720;

    // Recursos Gráficos
    SpriteFont fuente;
    Texture2D mcMale, mcFemale, mcSeleccionado;
    Texture2D puntoBlanco, circuloBorde;
    Texture2D texHierbaSheet, texCaminoSheet; 
    
    // Datos
    List<Personaje> pokedex = new List<Personaje>();
    TiledMap mapaPueblo;
    
    // Estados y Control
    GameState estadoActual = GameState.Menu;
    MouseState ratonAnterior;
    KeyboardState tecladoAnterior;
    
    // Jugador
    string nombreJugador = "";
    Vector2 posJugador = new Vector2(300, 300);
    float velocidadJugador = 300f;
    bool menuLateralAbierto = false;
    int seleccionadoIndex = 0;

    // Animaciones y UI
    float alphaMale = 0f, alphaFemale = 0f;
    const float VelocidadAnimacion = 5f; 
    string inputNombre = "", inputTipo = "", inputDesc = "", inputImagen = "";
    int campoActivo = 0;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        _graphics.PreferredBackBufferWidth = VirtualWidth;
        _graphics.PreferredBackBufferHeight = VirtualHeight;
        _graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
        Window.TextInput += Window_TextInput;
        base.Initialize();
    }

    private void Window_TextInput(object sender, TextInputEventArgs e)
    {
        if (estadoActual == GameState.SeleccionPersonaje)
        {
            if (e.Key == Keys.Back && nombreJugador.Length > 0) nombreJugador = nombreJugador.Substring(0, nombreJugador.Length - 1);
            else if (fuente.Characters.Contains(e.Character) && nombreJugador.Length < 15) nombreJugador += e.Character;
        }
        else if (estadoActual == GameState.Anadir)
        {
            if (e.Key == Keys.Back)
            {
                if (campoActivo == 0 && inputNombre.Length > 0) inputNombre = inputNombre.Substring(0, inputNombre.Length - 1);
                else if (campoActivo == 1 && inputTipo.Length > 0) inputTipo = inputTipo.Substring(0, inputTipo.Length - 1);
                else if (campoActivo == 2 && inputDesc.Length > 0) inputDesc = inputDesc.Substring(0, inputDesc.Length - 1);
            }
            else if (fuente.Characters.Contains(e.Character))
            {
                if (campoActivo == 0) inputNombre += e.Character;
                else if (campoActivo == 1) inputTipo += e.Character;
                else if (campoActivo == 2) inputDesc += e.Character;
            }
        }
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _renderTarget = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        fuente = Content.Load<SpriteFont>("fuente");
        
        puntoBlanco = new Texture2D(GraphicsDevice, 1, 1);
        puntoBlanco.SetData(new[] { Color.White });

        int radius = 20; int diameter = radius * 2;
        circuloBorde = new Texture2D(GraphicsDevice, diameter, diameter);
        Color[] colorData = new Color[diameter * diameter];
        for (int x = 0; x < diameter; x++) {
            for (int y = 0; y < diameter; y++) {
                if (new Vector2(x - radius, y - radius).Length() <= radius) colorData[x + y * diameter] = Color.White;
                else colorData[x + y * diameter] = Color.Transparent;
            }
        }
        circuloBorde.SetData(colorData);

        mcMale = Texture2D.FromStream(GraphicsDevice, File.OpenRead("src/MainCharacters/MC_Male.png"));
        mcFemale = Texture2D.FromStream(GraphicsDevice, File.OpenRead("src/MainCharacters/MC_Female.png"));
        texHierbaSheet = Texture2D.FromStream(GraphicsDevice, File.OpenRead("src/Tileset/hierbaTile.png"));
        texCaminoSheet = Texture2D.FromStream(GraphicsDevice, File.OpenRead("src/Tileset/caminoTile.png"));

        string rutaMapa = "src/Mapas/pueblo.tmj";
        if (File.Exists(rutaMapa)) {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            mapaPueblo = JsonSerializer.Deserialize<TiledMap>(File.ReadAllText(rutaMapa), options);
        }

        CargarPokedex();
    }

    private void CargarPokedex()
    {
        pokedex.Clear();
        string ruta = "src/Pokedex";
        if (!Directory.Exists(ruta)) Directory.CreateDirectory(ruta);
        foreach (string png in Directory.GetFiles(ruta, "*.png"))
        {
            Personaje p = new Personaje();
            using (var stream = File.OpenRead(png)) p.Textura = Texture2D.FromStream(GraphicsDevice, stream);
            string txt = png.Replace(".png", ".txt");
            if (File.Exists(txt)) {
                string[] lineas = File.ReadAllLines(txt);
                p.Nombre = lineas.Length > 0 ? lineas[0] : "???";
                p.Tipo = lineas.Length > 1 ? lineas[1] : "???";
                p.Descripcion = lineas.Length > 2 ? string.Join("\n", lineas, 2, lineas.Length - 2) : "";
            }
            pokedex.Add(p);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        CalculateScreenDestination();
        MouseState mouse = Mouse.GetState();
        KeyboardState kbd = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        float scaleX = (float)VirtualWidth / _screenDestination.Width;
        float scaleY = (float)VirtualHeight / _screenDestination.Height;
        Point posRaton = new Point((int)((mouse.X - _screenDestination.X) * scaleX), (int)((mouse.Y - _screenDestination.Y) * scaleY));
        bool clic = mouse.LeftButton == ButtonState.Pressed && ratonAnterior.LeftButton == ButtonState.Released;

        int cx = VirtualWidth / 2;
        int cy = VirtualHeight / 2;

        switch (estadoActual)
        {
            case GameState.Menu:
                if (clic && new Rectangle(cx - 100, cy - 50, 200, 40).Contains(posRaton)) estadoActual = GameState.SeleccionPersonaje;
                if (clic && new Rectangle(cx - 100, cy + 10, 200, 40).Contains(posRaton)) estadoActual = GameState.Opciones;
                break;

            case GameState.SeleccionPersonaje:
                Rectangle rM = new Rectangle(cx - 550, 120, 450, 550);
                Rectangle rF = new Rectangle(cx + 100, 120, 450, 550);
                
                alphaMale = MathHelper.Clamp(alphaMale + (rM.Contains(posRaton) || mcSeleccionado == mcMale ? 1 : -1) * dt * VelocidadAnimacion, 0f, 1f);
                alphaFemale = MathHelper.Clamp(alphaFemale + (rF.Contains(posRaton) || mcSeleccionado == mcFemale ? 1 : -1) * dt * VelocidadAnimacion, 0f, 1f);

                if (clic && rM.Contains(posRaton)) mcSeleccionado = mcMale;
                if (clic && rF.Contains(posRaton)) mcSeleccionado = mcFemale;
                if (clic && mcSeleccionado != null && nombreJugador.Length > 0 && new Rectangle(cx - 100, 670, 200, 50).Contains(posRaton)) 
                    estadoActual = GameState.Exploracion;
                break;

            case GameState.Exploracion:
                Vector2 mov = Vector2.Zero;
                if (kbd.IsKeyDown(Keys.W)) mov.Y--; if (kbd.IsKeyDown(Keys.S)) mov.Y++;
                if (kbd.IsKeyDown(Keys.A)) mov.X--; if (kbd.IsKeyDown(Keys.D)) mov.X++;

                if (mov != Vector2.Zero) {
                    mov.Normalize();
                    Vector2 nPos = posJugador + mov * velocidadJugador * dt;
                    if (!CheckCollision(nPos)) posJugador = nPos;
                }

                if (kbd.IsKeyDown(Keys.X) && tecladoAnterior.IsKeyUp(Keys.X)) menuLateralAbierto = !menuLateralAbierto;
                if (menuLateralAbierto && clic && new Rectangle(VirtualWidth - 270, 80, 250, 60).Contains(posRaton)) {
                    estadoActual = GameState.Lista; menuLateralAbierto = false;
                }
                break;

            case GameState.Lista:
                for (int i = 0; i < pokedex.Count; i++)
                    if (clic && new Rectangle(900, 150 + (i * 45), 500, 35).Contains(posRaton)) { seleccionadoIndex = i; estadoActual = GameState.Pokedex; }
                if (clic && new Rectangle(VirtualWidth - 150, VirtualHeight - 70, 130, 50).Contains(posRaton)) estadoActual = GameState.Exploracion;
                break;

            case GameState.Pokedex:
                if (clic && new Rectangle(900, 620, 200, 50).Contains(posRaton)) estadoActual = GameState.Lista;
                break;

            case GameState.Opciones:
                if (clic && new Rectangle(cx - 100, cy, 250, 40).Contains(posRaton)) estadoActual = GameState.Anadir;
                if (clic && new Rectangle(cx - 50, cy + 100, 100, 40).Contains(posRaton)) estadoActual = GameState.Menu;
                break;

            case GameState.Anadir:
                if (clic && new Rectangle(cx - 110, cy + 150, 100, 40).Contains(posRaton) && File.Exists(inputImagen)) {
                    string f = Path.GetFileName(inputImagen);
                    File.Copy(inputImagen, Path.Combine("src", "Pokedex", f), true);
                    File.WriteAllText(Path.Combine("src", "Pokedex", Path.GetFileNameWithoutExtension(f) + ".txt"), $"{inputNombre}\n{inputTipo}\n{inputDesc}");
                    CargarPokedex();
                    estadoActual = GameState.Opciones;
                }
                if (clic && new Rectangle(cx + 10, cy + 150, 100, 40).Contains(posRaton)) estadoActual = GameState.Opciones;
                break;
        }

        ratonAnterior = mouse;
        tecladoAnterior = kbd;
        base.Update(gameTime);
    }

    private bool CheckCollision(Vector2 p) {
        if (mapaPueblo == null) return false;
        int tx = (int)(p.X + 40) / 64;
        int ty = (int)(p.Y + 90) / 64;
        foreach (var l in mapaPueblo.layers)
            if (l.name == "Colisiones" && tx >= 0 && ty >= 0 && tx < mapaPueblo.width && ty < mapaPueblo.height)
                if (l.data[tx + ty * mapaPueblo.width] != 0) return true;
        return false;
    }

    private void DrawRoundedRect(Rectangle rect, Color color, int radius) {
        _spriteBatch.Draw(puntoBlanco, new Rectangle(rect.X + radius, rect.Y, rect.Width - radius * 2, rect.Height), color);
        _spriteBatch.Draw(puntoBlanco, new Rectangle(rect.X, rect.Y + radius, rect.Width, rect.Height - radius * 2), color);
        _spriteBatch.Draw(circuloBorde, new Rectangle(rect.X, rect.Y, radius * 2, radius * 2), color);
        _spriteBatch.Draw(circuloBorde, new Rectangle(rect.X + rect.Width - radius * 2, rect.Y, radius * 2, radius * 2), color);
        _spriteBatch.Draw(circuloBorde, new Rectangle(rect.X, rect.Y + rect.Height - radius * 2, radius * 2, radius * 2), color);
        _spriteBatch.Draw(circuloBorde, new Rectangle(rect.X + rect.Width - radius * 2, rect.Y + rect.Height - radius * 2, radius * 2, radius * 2), color);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(new Color(25, 35, 40));
        _spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp);
        int cx = VirtualWidth / 2, cy = VirtualHeight / 2;

        switch (estadoActual)
        {
            case GameState.Exploracion:
                if (mapaPueblo != null) {
                    foreach (var layer in mapaPueblo.layers) {
                        for (int i = 0; i < layer.data.Count; i++) {
                            int gid = layer.data[i];
                            if (gid == 0) continue;
                            
                            Texture2D sheet = gid >= 1427 ? texCaminoSheet : texHierbaSheet;
                            int fGid = gid >= 1427 ? 1427 : 1;
                            int cols = gid >= 1427 ? 12 : 46;

                            int loc = gid - fGid;
                            Rectangle src = new Rectangle((loc % cols) * 64, (loc / cols) * 64, 64, 64);
                            _spriteBatch.Draw(sheet, new Rectangle((i % mapaPueblo.width) * 64, (i / mapaPueblo.width) * 64, 64, 64), src, Color.White);
                        }
                    }
                }
                _spriteBatch.Draw(mcSeleccionado, new Rectangle((int)posJugador.X, (int)posJugador.Y, 80, 100), Color.White);
                
                if (menuLateralAbierto) {
                    Rectangle mR = new Rectangle(VirtualWidth - 320, 20, 300, VirtualHeight - 40);
                    _spriteBatch.Draw(puntoBlanco, new Rectangle(mR.X - 8, mR.Y + 8, mR.Width, mR.Height), Color.Black * 0.4f);
                    DrawRoundedRect(mR, Color.White, 20);
                    _spriteBatch.DrawString(fuente, "POKEDEX", new Vector2(VirtualWidth - 270, 100), Color.Black);
                }
                break;

            case GameState.Menu:
                _spriteBatch.DrawString(fuente, "COMENZAR", new Vector2(cx - 70, cy - 50), Color.White);
                _spriteBatch.DrawString(fuente, "OPCIONES", new Vector2(cx - 70, cy + 10), Color.White);
                break;

            case GameState.SeleccionPersonaje:
                _spriteBatch.DrawString(fuente, "ELIGE TU PERSONAJE", new Vector2(cx - 150, 50), Color.Yellow);
                _spriteBatch.Draw(mcMale, new Rectangle(cx - 550, 120, 450, 550), Color.Lerp(Color.Black * 0.7f, Color.White, alphaMale));
                _spriteBatch.Draw(mcFemale, new Rectangle(cx + 100, 120, 450, 550), Color.Lerp(Color.Black * 0.7f, Color.White, alphaFemale));
                _spriteBatch.DrawString(fuente, "NOMBRE: " + nombreJugador + "_", new Vector2(cx - 100, 640), Color.White);
                if (mcSeleccionado != null && nombreJugador.Length > 0) _spriteBatch.DrawString(fuente, "[ CONFIRMAR ]", new Vector2(cx - 100, 680), Color.GreenYellow);
                break;

            case GameState.Lista:
            case GameState.Pokedex:
                _spriteBatch.Draw(mcSeleccionado, new Rectangle(80, 40, 550, 640), Color.White);
                if (estadoActual == GameState.Lista) {
                    _spriteBatch.DrawString(fuente, "POKEDEX DISPONIBLE", new Vector2(900, 100), Color.Cyan);
                    for (int i = 0; i < pokedex.Count; i++) _spriteBatch.DrawString(fuente, $"{i + 1}. {pokedex[i].Nombre}", new Vector2(900, 150 + (i * 45)), Color.White);
                    _spriteBatch.DrawString(fuente, "SALIR", new Vector2(VirtualWidth - 150, VirtualHeight - 70), Color.Red);
                } else {
                    var p = pokedex[seleccionadoIndex];
                    _spriteBatch.Draw(p.Textura, new Rectangle(900, 80, 350, 350), Color.White);
                    _spriteBatch.DrawString(fuente, $"NOMBRE: {p.Nombre}\nTIPO: {p.Tipo}\n\n{p.Descripcion}", new Vector2(900, 450), Color.White);
                    _spriteBatch.DrawString(fuente, "[ VOLVER ]", new Vector2(900, 620), Color.Yellow);
                }
                break;

            case GameState.Opciones:
                _spriteBatch.DrawString(fuente, "AÑADIR POKEMON", new Vector2(cx - 100, cy), Color.White);
                _spriteBatch.DrawString(fuente, "VOLVER", new Vector2(cx - 50, cy + 100), Color.Red);
                break;
        }

        _spriteBatch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, _screenDestination, Color.White);
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void CalculateScreenDestination() {
        float target = (float)VirtualWidth / VirtualHeight;
        float window = (float)Window.ClientBounds.Width / Window.ClientBounds.Height;
        if (window > target) {
            int w = (int)(Window.ClientBounds.Height * target);
            _screenDestination = new Rectangle((Window.ClientBounds.Width - w) / 2, 0, w, Window.ClientBounds.Height);
        } else {
            int h = (int)(Window.ClientBounds.Width / target);
            _screenDestination = new Rectangle(0, (Window.ClientBounds.Height - h) / 2, Window.ClientBounds.Width, h);
        }
    }
}