using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using System.Windows.Automation.Provider;
using System.Net.WebSockets;

namespace GeoWpf
{
    public partial class GameWindow : Window
    {
        private const double PlayerWidth = 50;
        private const double PlayerHeight = 50;
        private const double PlatformWidth = 100;
        private const double PlatformHeight = 10;
        private const double Gravity = 1.0;
        private const double MaxJumpSpeed = 25.0;
        private const double MoveSpeed = 5.0;
        private double PlatformSpacing; //= 150; // Horizontal spacing between platforms
        private Rectangle? player;
        private double playerVelocityX;
        private double playerVelocityY;
        private bool isJumping;
        private bool gameOver;
        private Rectangle? jumpForceBar;
        private DateTime spaceKeyPressedTime;
        private bool spaceKeyHeldDown;
        private const double CanvasTopBoundary = 0;
        private List<Rectangle> ?platforms;
        private List<Rectangle>? enemies;
        private Random ?random;
        private int score;
        private DispatcherTimer ?gameTimer;
        private bool takingShield;

        public GameWindow()
        {
            this.Background = new SolidColorBrush(Color.FromRgb(30, 57, 71));
            InitializeComponent();
           //InitializeGame();
        }

        private void InitializeGame()
        {
            gameOver = false;
            takingShield = false;
            score = 0;
            jumpForceBar = new Rectangle();
            jumpForceBar = JumpForceBar;
            platforms = new List<Rectangle>();
            enemies = new List<Rectangle>();
            random = new Random();
            GenerateInitialPlatforms();

            player = new Rectangle
            {
                Width = PlayerWidth,
                Height = PlayerHeight
            };

            Canvas.SetLeft(player, 0);
            Canvas.SetTop(player, 0);
            GameCanvas.Children.Add(player);

            playerVelocityX = MoveSpeed;
            playerVelocityY = 0;

            lblScore.Content = $"Score: {score}";

            gameTimer = new DispatcherTimer();
            gameTimer.Interval = TimeSpan.FromMilliseconds(20);
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            KeyDown += GameWindow_KeyDown;
            KeyUp += GameWindow_KeyUp;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            startUp.Visibility = Visibility.Collapsed;
            GameGrid.Visibility = Visibility.Visible;
            
            Background = new SolidColorBrush(Colors.White);
            InitializeGame();
        }

        private void GenerateInitialPlatforms()
        {
            platforms.Clear();

            for (int i = 0; i < 3; i++)
            {
                double left = i * 100;

                CreatePlatform(left, 200);
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (!gameOver)
            {
                score++;
                lblScore.Content = $"Score: {score}";
                playerVelocityY += Gravity;

                double newX = Canvas.GetLeft(player) + playerVelocityX;
                double newY = Canvas.GetTop(player) + playerVelocityY;

                if (newY <= CanvasTopBoundary)
                {
                    // Reverse player's vertical velocity to simulate bounce
                    playerVelocityY = -playerVelocityY;
                    newY = CanvasTopBoundary; // Ensure player stays within canvas boundaries
                }

                Canvas.SetLeft(player, newX);
                Canvas.SetTop(player, newY);

                bool onAnyPlatform = false;

                if (isJumping&&!takingShield)
                {
                    player.Fill = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(@"pack://application:,,,/Images/stickman2.jpg", UriKind.Absolute))
                    };
                }
                else if (takingShield)
                {
                    player.Fill = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(@"pack://application:,,,/Images/stickman3.jpg", UriKind.Absolute))
                    };
                }
                else
                {
                    player.Fill = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(@"pack://application:,,,/Images/stickman1.png", UriKind.Absolute))
                    };
                }

                double leftEdgeThreshold = 20; // Define a threshold for how close to the left edge the enemy can get
                bool enemyMoved = false; // Flag to track if an enemy has been moved

                foreach (var platform in platforms)
                {
                    if (IsPlayerOnPlatform(player, platform))
                    {
                        if (platform.ActualWidth > 300) // Check if it's a large platform
                        {
                            foreach (var en in enemies)
                            {
                                if (!enemyMoved && Canvas.GetLeft(en) >= Canvas.GetLeft(platform) && Canvas.GetLeft(en) <= Canvas.GetLeft(platform) + platform.ActualWidth)
                                {
                                    double left = Canvas.GetLeft(en);
                                    double platformLeft = Canvas.GetLeft(platform); // Get the left edge of the current platform
                                    double platformRight = platformLeft + platform.ActualWidth; // Get the right edge of the current platform
                                    double middleOfPlatform = platformLeft + platform.ActualWidth / 2; // Calculate the middle of the platform
                                    bool isEnemyOnPlatform = left >= platformLeft && left <= platformRight;

                                    // If the enemy is too close to the left edge, make it move back to the middle
                                    if (left < platformLeft + leftEdgeThreshold)
                                    {
                                        left += 5; // Move right towards the middle
                                    }
                                    else if (left > platformRight - leftEdgeThreshold)
                                    {
                                        left -= 5; // Move left towards the middle
                                    }
                                    else
                                    {
                                        // Otherwise, move towards the player
                                        if (left > Canvas.GetLeft(player))
                                        {
                                            left -= 5; // Move left towards the player
                                        }
                                        else if (left < Canvas.GetLeft(player))
                                        {
                                            left += 5; // Move right towards the player
                                        }
                                    }

                                    Canvas.SetLeft(en, left);

                                    // Check collision with player
                                    if (IsCollision(player, en))
                                    {
                                        if (!takingShield) GameOver();
                                        // Remove enemy from canvas and list
                                        GameCanvas.Children.Remove(en);
                                        enemies.Remove(en);
                                        break; // Exit loop since enemy is removed
                                    }

                                    enemyMoved = true; // Set flag to true as we have moved the enemy
                                }
                            }
                        }

                        onAnyPlatform = true;
                        playerVelocityY = 0;
                        isJumping = false; // Reset jumping flag
                        Canvas.SetTop(player, Canvas.GetTop(platform) - PlayerHeight);
                        break; // Exit loop since player is only on one platform at a time
                    }
                }


                if (spaceKeyHeldDown)
                {
                    TimeSpan heldDuration = DateTime.Now - spaceKeyPressedTime;
                    double jumpStrength = Math.Min(heldDuration.TotalMilliseconds / 1000.0, 1.0); // Maximum 1 second
                    UpdateJumpForceBarWidth(jumpStrength);
                }

                // Check for falling off the screen
                if (!onAnyPlatform && Canvas.GetTop(player) > GameCanvas.ActualHeight)
                {
                    GameOver();
                }

                // Center the player on the canvas
                ScrollCanvas();

                // Generate new platform if necessary
                if (newX > Canvas.GetLeft(platforms.Last()) + PlatformWidth - 400) // Trigger platform generation earlier
                {
                    GenerateNewPlatform();
                }

                // Remove platforms that have moved off the screen
                RemoveOffScreenPlatforms();
            }
        }

        private bool IsCollision (Rectangle rect1, Rectangle rect2)
        {
            Rect playerBounds = new Rect(Canvas.GetLeft(rect1), Canvas.GetTop(rect1), rect1.ActualWidth, rect1.ActualHeight);
            Rect enemyBounds = new Rect(Canvas.GetLeft(rect2), Canvas.GetTop(rect2), rect2.ActualWidth, rect2.ActualHeight);

            return playerBounds.IntersectsWith(enemyBounds);
        }

        private void ScrollCanvas()
        {
            double playerCenterX = Canvas.GetLeft(player) + PlayerWidth / 2;
            double canvasCenterX = GameCanvas.ActualWidth / 2;

            double offsetX = playerCenterX - canvasCenterX;

            foreach (var child in GameCanvas.Children.OfType<UIElement>())
            {
                double left = Canvas.GetLeft(child) - offsetX-100;
                Canvas.SetLeft(child, left);
            }
        }

        private bool IntersectsPlayer(Rectangle topRectangle, Rect playerBounds)
        {
            // Get the bounds of the topRectangle
                Rect topRectangleBounds = new Rect(
                Canvas.GetLeft(topRectangle),
                Canvas.GetTop(topRectangle),
                topRectangle.Width,
                topRectangle.Height);

            // Check if there is an intersection between topRectangleBounds and playerBounds
            return topRectangleBounds.IntersectsWith(playerBounds);
        }

        private bool IsPlayerOnPlatform(Rectangle player, Rectangle platform)
        {
            double playerBottom = Canvas.GetTop(player) + PlayerHeight;
            double platformTop = Canvas.GetTop(platform);
            double platformBottom = platformTop + PlatformHeight;
            double playerRight = Canvas.GetLeft(player) + PlayerWidth;
            double platformLeft = Canvas.GetLeft(platform);
            double platformRight = platformLeft + platform.Width;

            // Check if player's bottom is near platform's top and within platform's vertical bounds
            bool verticalCollision = playerBottom >= platformTop && playerBottom <= platformBottom + Math.Abs(playerVelocityY);

            // Check if player's right side is to the left of platform's right side and player's left side is to the right of platform's left side
            bool horizontalCollision = playerRight >= platformLeft && Canvas.GetLeft(player) <= platformRight;

            // Check if player is on top of the platform and not colliding from the sides
            bool onTopOfPlatform = verticalCollision && horizontalCollision;

            double playerTop = Canvas.GetTop(player);
            double platformGap = 3; // Adjust this value as needed

            // Check if player's top is slightly above the platform's bottom
            bool canJump = !onTopOfPlatform || (playerBottom >= platformTop && playerTop <= platformBottom + platformGap);

            return onTopOfPlatform && canJump;
        }

        private void GenerateNewPlatform()
        {
            double lastPlatformLeft = Canvas.GetLeft(platforms.Last());
            double newLeft = lastPlatformLeft + PlatformSpacing;
            double newTop = random.Next((int)(GameCanvas.ActualHeight - PlatformHeight - 200), (int)(GameCanvas.ActualHeight - PlatformHeight - 50));

            CreatePlatform(newLeft, newTop);
        }

        private void CreatePlatform(double left, double top)
        {
            int n = random.Next(1, 6);

            //If n is 1 then a large platform will be created
            if (n == 1 && score > 100)
            {

                Rectangle platform = new Rectangle
                {
                    Width = PlatformWidth + 280,
                    Height = PlatformHeight,
                    Fill = Brushes.Green,
                    RadiusX = 1,
                    RadiusY = 1
                };
                
                Canvas.SetLeft(platform, left);
                Canvas.SetTop(platform, top);

                Rectangle topRectangle = new Rectangle
                {
                    Width = 50,
                    Height = 50,
                    Fill = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(@"pack://application:,,,/Images/stickman4.jpg", UriKind.Absolute))
                    },
                };

                Canvas.SetLeft(topRectangle, left + (platform.Width - topRectangle.Width)/ 2); // Center horizontally
                Canvas.SetTop(topRectangle, top - topRectangle.Height); // Place just above the platform

                GameCanvas.Children.Add(topRectangle);
                GameCanvas.Children.Add(platform);
                platforms.Add(platform);
                enemies.Add(topRectangle);

                PlatformSpacing = 430;

            }
            else
            {
                Rectangle platform = new Rectangle
                {
                    Width = PlatformWidth,
                    Height = PlatformHeight,
                    Fill = Brushes.Green,
                    RadiusX = 1,
                    RadiusY = 1
                };

                Canvas.SetLeft(platform, left);
                Canvas.SetTop(platform, top);

                GameCanvas.Children.Add(platform);
                platforms.Add(platform);
                PlatformSpacing = 150;
            }

        }

        private void RemoveOffScreenPlatforms()
        {
            List<Rectangle> platformsToRemove = new List<Rectangle>();
            List<Rectangle> enemiesToRemove = new List<Rectangle>();

            foreach (var platform in platforms)
            {
                double platformRight = Canvas.GetLeft(platform) + PlatformWidth;

                if (platformRight < -300)
                {
                    platformsToRemove.Add(platform);
                }
            }

            foreach (var platform in platformsToRemove)
            {
                platforms.Remove(platform);
                GameCanvas.Children.Remove(platform);
            }
        }

        private void GameWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && !isJumping && !spaceKeyHeldDown &&!takingShield)
            {
                spaceKeyPressedTime = DateTime.Now;
                spaceKeyHeldDown = true;
            }
            else if((e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl) && !isJumping )
            {
                takingShield = true;
            }
            
        }

        private void GameWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && spaceKeyHeldDown)
            {
                spaceKeyHeldDown = false;

                TimeSpan heldDuration = DateTime.Now - spaceKeyPressedTime;
                double jumpStrength = Math.Min(heldDuration.TotalMilliseconds / 1000.0, 1.0); // Maximum 1 second

                // If the key was pressed for less than 100ms, set a minimum jump strength
                if (heldDuration.TotalMilliseconds < 100) // Quick tap detection
                {
                    jumpStrength = 0.4; // Set minimum jump strength
                }

                // First update to reflect the jump strength in the jump force bar
                UpdateJumpForceBarWidth(jumpStrength);

                playerVelocityY = -MaxJumpSpeed * jumpStrength;
                isJumping = true;

                // After a brief delay, reset the jump force bar
                Task.Delay(200).ContinueWith(_ => UpdateJumpForceBarWidth(0)); // Reset after a short delay
            }

            else if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                takingShield = false;
            }
        }

        private void UpdateJumpForceBar()
        {
            // Calculate held duration
            TimeSpan heldDuration = DateTime.Now - spaceKeyPressedTime;
            double jumpStrength = Math.Min(heldDuration.TotalMilliseconds / 1000.0, 1.0); // Maximum 1 second
            
            UpdateJumpForceBarWidth(jumpStrength);
        }

        private void UpdateJumpForceBarWidth(double jumpStrength)
        {
            double maxWidth = 300; // Adjust as needed

            // Set the width of the jump force bar based on jump strength
            double newWidth = jumpStrength * maxWidth;

            // Ensure the UI update happens on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                jumpForceBar.Width = newWidth;
            });
        }

        private void GameOver()
        {
            gameOver = true;
            GameCanvas.Children.Clear();
            //gameTimer.Tick -= GameLoop;

            GameGrid.Visibility = Visibility.Collapsed;
            GameOverGrid.Visibility = Visibility.Visible;
            GameOverGrid.Background = new SolidColorBrush(Color.FromRgb(30, 57, 71));

            //KeyDown -= GameWindow_KeyDown;
            //KeyUp -= GameWindow_KeyUp;


            lblResult.Content = $"Game over\nYour score was {score}\nPlay again?";

            playAgainBtn.Click += (o, e) =>
            {
                gameTimer.Stop();
                InitializeGame();
                GameOverGrid.Visibility = Visibility.Collapsed;
                GameGrid.Visibility = Visibility.Visible;
            };

            exitBtn.Click += (o, e) => Close();
        }
    }
}