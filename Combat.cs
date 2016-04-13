using GridMasters.Characters;
using GridMasters.CombatUtils;
using GridMasters.Utilities.Controllers;
using GridMasters.Utilities;
using GridMasters.Utilities.AssetLoaders;
using GridMasters.Characters.Decimus;
using GridMasters.Characters.SRA;
using GridMasters.Characters.Malaya;
using GridMasters.Characters.Scrapper;
using GridMasters.Utilities.Graphics;
using GridMasters.CharacterSelect;
using GridMasters.Utilities.Network.Messages;
using GridMasters.Utilities.Network;
using GridMasters.Utilities.Network.Messages.Combat;

namespace GridMasters.Loops.CombatLoops
{
    class Combat : Loop
    {
        protected bool online;

        static Tile[,] tileGrid;
        protected static Character[] characters;
        
        PositionedSprite background;

        public Combat( bool[] lockedSlots, bool isOnline, bool isTraining)
        {
            online = isOnline;
            bool soloRed = (lockedSlots[0] != lockedSlots[1]);
            bool soloBlue = (lockedSlots[3] != lockedSlots[4]);
            characters = new Character[4];

            CombatAssets.generateTiles();
            tileGrid = CombatAssets.getTiles();

            #region PlayerCharacterInitialization
            foreach (PlayerData player in LoopManager.playerDataList)
            {
                if (player.state == Player.State.Ready || player.state == Player.State.Remote)
                {
                    Team team = Team.Red;
                    int healthSlot = 0;
                    byte startX = 1;
                    byte startY = 1;
                    
                    if (player.slot < Slot.Neutral)
                    {
                        healthSlot = (soloRed) ? 5 : (int)player.slot + 1;
                    }
                    else
                    {
                        team = Team.Blue;
                        startX = 4;
                        
                        healthSlot = (soloBlue) ? 6 : (int)player.slot;
                    }

                    if (healthSlot == 2 || healthSlot == 4)
                    {
                        startY = 2;
                    }
                    
                    switch (player.characterName)
                    {
                        case CharacterName.Decimus:
                            characters[(byte)player.playerNumber] = new Decimus(player.playerNumber, team, startX, startY, healthSlot, player.local, isTraining, player.controllerType==PlayerData.ControlType.DUMMY);
                            break;
                        case CharacterName.Malaya:
                            characters[(byte)player.playerNumber] = new Malaya(player.playerNumber, team, startX, startY, healthSlot, player.local, isTraining, player.controllerType == PlayerData.ControlType.DUMMY);
                            break;
                        case CharacterName.SRA:
                            characters[(byte)player.playerNumber] = new SRA(player.playerNumber, team, startX, startY, healthSlot, player.local, isTraining, player.controllerType == PlayerData.ControlType.DUMMY);
                            break;
                        case CharacterName.Scrapper:
                            characters[(byte)player.playerNumber] = new Scrapper(player.playerNumber, team, startX, startY, healthSlot, player.local, isTraining, player.controllerType == PlayerData.ControlType.DUMMY);
                            break;
                    }
                    player.state = Player.State.CharacterSelect;
                }
                else
                {
                    player.state = Player.State.Start;
                    player.slot = Slot.Neutral;
                    player.characterName = CharacterName.Decimus;
                }
            }
            #endregion

            DrawProperties bgLoc = new DrawProperties(0f, 0f, 6f, 3.375f);
            bgLoc.depth = 0f;
            background = new PositionedSprite(CombatAssets.getBackground(),bgLoc);
            
            CombatAssets.LoadCombatSounds();
        }

        public override void Update()
        {
            //MAKES ALL PLAYERS ANIMATE AND UPDATE.  KEEP RUNNING DURING INTRO/WIN SCREENS, TO KEEP PROJECTILES AND CHARACTERS ANIMATING.
            for (int i = 0; i < 4; i++)
            {
                if (characters[i] != null)
                {
                    characters[i].Update();
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (characters[i] != null)
                {
                    characters[i].UpdateProjectiles();
                }
            }
            UpdateAllTiles();      
        }

        public override void Draw()
        {
            background.Draw();
            DrawAllTiles();
            DrawAllCharacters();
        }

        //Only takes inputes from controllers currently connected to players.
        public override void ActiveInput(PlayerNumber player, Button input)
        {
            characters[(byte)player].Input(input);
        }

        //handles recieved online messages.
        public override void NetMessage(Message msg)
        {
            switch (msg.type)
            {
                case MessageType.Health:
                    HealthMessage hm = (HealthMessage)msg;
                    characters[(int)hm.player].NetHealth(hm.currentHealth,hm.currentLives);
                    break;
                case MessageType.PlayerState:
                    PlayerStateMessage psm = (PlayerStateMessage)msg;
                    characters[(int)psm.player].NetState(psm.state, 0, psm.x, psm.y);
                    break;
                case MessageType.PlayerStatusEffect:
                    PlayerStatusEffectMessage psem = (PlayerStatusEffectMessage)msg;
                    characters[(int)psem.player].NetStatus(psem.status, psem.effectDuration);
                    break;
                case MessageType.Disconnect:
                    DisconnectMessage dm = (DisconnectMessage)msg;
                    if(dm.isHost == true)
                    {
                        NetworkManager.Disconnect();
                        LoopManager.newLoop(new Title());
                        LoopManager.SetMessage("Disconnected from host.  Returning to title.");
                        CombatAssets.combatMusic.Stop();
                    }
                    break;
                case MessageType.Signal:
                    SignalMessage sm = (SignalMessage)msg;
                    characters[(int)sm.player].NetSignal(sm.signalValue);
                    break;
                case MessageType.DeactivatePlayer:
                    DeactivatePlayerMessage dpm = (DeactivatePlayerMessage)msg;
                    characters[(int)dpm.player].Disconnect();
                    LoopManager.playerDataList[(int)dpm.player]= new PlayerData((PlayerNumber)dpm.player);
                    break;
            }
        }

        
        public void UpdateAllTiles()
        {
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    tileGrid[x, y].Update();
                }
            }
        }

        #region SimpleDrawCycles
        public void DrawAllTiles()
        {
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    tileGrid[x, y].Draw();
                }
            }
        }

        private void DrawAllCharacters()
        {

            for (int i = 0; i < 4; i++)
            {
                if (characters[i] != null)
                {
                    characters[i].Draw();
                }
            }
        }

        private void DrawCharacter(int player)
        {
            characters[player].Draw();
        }
        #endregion

        #region AttackChecks
        // Applies damage to tile, dealing damage to any characters on it.  Returns TRUE if damage collides with character, so that the projectile may delete itself if neccesary.
        public static bool damage(Tile tile, int dmg, Team team)
        {
            bool collide = false;
            if (tile != null)
            {
                tile.setDanger(); 
                for (int i = 0; i < 4; i++)
                {
                    if (characters[i] != null)
                    {
                        collide = (characters[i].checkCollision(tile.x, tile.y, dmg, team) || collide);
                    }
                }
            }
            return collide;
        }
        public static bool damage(int x, int y, int dmg, Team team)
        {
            tileGrid[x,y].setDanger();
            bool collide = false;
            for (int i = 0; i < 4; i++)
            {
                if (characters[i] != null)
                {
                    collide = (characters[i].checkCollision(x, y, dmg, team) || collide);
                }
            }
            return collide;
        }
        
        public static bool statusEffect(int x, int y, StatusEffect type, int time, Team team)
        {
            tileGrid[x, y].setDanger();
            bool collide = false;
            for (int i = 0; i < 4; i++)
            {
                if (characters[i] != null)
                {
                    if (characters[i].checkCollision(x, y, 0, team))
                    {
                        collide = true;
                        characters[i].setStatus(x, y, time, team, type);
                    }
                }
            }
            return collide;
        }
        #endregion

    }
}