﻿using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Tools;
using System;
using System.Collections.Generic;

namespace PrimevalTitmouse
{
    public class Body
    {
        //Lets think of Food in Calories, and water in mL
        //For a day Laborer (like a farmer) that should be ~3500 Cal, and 14000 mL
        //Of course this is dependant on amount of work, but let's go one step at a time
        private static readonly float requiredCaloriesPerDay = 3500f;
        private static readonly float requiredWaterPerDay = 14000f;
        private static readonly float maxWaterInCan = 4000f; //How much water does the wattering can hold? Max is 40, so *100

        //Average # of Pees per day is ~6.
        private static readonly float maxBladderCapacity = requiredWaterPerDay / 6f;
        private static readonly float minBladderCapacity = maxBladderCapacity * 0.20f;
        private static readonly float bladderAttemptThreshold = maxBladderCapacity * 0.1f;
        private static readonly float bladderTrainingThreshold = maxBladderCapacity * 0.5f;

        //Average # of poops per day varies wildly. Let's say once per day.
        private static readonly float maxBowelCapacity = requiredCaloriesPerDay / 1f;
        private static readonly float minBowelCapacity = maxBowelCapacity * 0.20f;
        private static readonly float bowelAttemptThreshold = maxBowelCapacity * 0.1f;
        private static readonly float bowelTrainingThreshold = maxBowelCapacity * 0.5f;

        //Setup Thresholds and messages
        private static readonly float[] WETTING_THRESHOLDS = { 0.1f, 0.3f, 0.5f };
        private static readonly string[][] WETTING_MESSAGES = { Regression.t.Bladder_Red, Regression.t.Bladder_Orange, Regression.t.Bladder_Yellow };
        private static readonly float[] MESSING_THRESHOLDS = { 0.1f, 0.3f, 0.5f };
        private static readonly string[][] MESSING_MESSAGES = { Regression.t.Bowels_Red, Regression.t.Bowels_Orange, Regression.t.Bowels_Yellow };
        private static readonly float[] BLADDER_CONTINENCE_THRESHOLDS = { 0.6f, 0.2f, 0.5f, 0.8f };
        private static readonly string[][] BLADDER_CONTINENCE_MESSAGES = { Regression.t.Bladder_Continence_Min, Regression.t.Bladder_Continence_Red, Regression.t.Bladder_Continence_Orange, Regression.t.Bladder_Continence_Yellow };
        private static readonly float[] BOWEL_CONTINENCE_THRESHOLDS = { 0.6f, 0.2f, 0.5f, 0.8f };
        private static readonly string[][] BOWEL_CONTINENCE_MESSAGES = { Regression.t.Bowel_Continence_Min, Regression.t.Bowel_Continence_Red, Regression.t.Bowel_Continence_Orange, Regression.t.Bowel_Continence_Yellow };
        private static readonly float[] HUNGER_THRESHOLDS = { 0.0f, 0.25f };
        private static readonly string[][] HUNGER_MESSAGES = { Regression.t.Food_None, Regression.t.Food_Low };
        private static readonly float[] THIRST_THRESHOLDS = { 0.0f, 0.25f };
        private static readonly string[][] THIRST_MESSAGES = { Regression.t.Water_None, Regression.t.Water_Low };
        private static readonly int MESSY_DEBUFF = 222;
        private static readonly int WET_DEBUFF = 111;

        //Things that describe an individual
        public float bladderCapacity = maxBladderCapacity;
        public float bladderContinence = 1f;
        public float bladderFullness = 0f;
        public float bowelCapacity = maxBowelCapacity;
        public float bowelContinence = 1f;
        public float bowelFullness = 0f;
        public float hunger = 0f;
        public float thirst = 0f;
        public bool isSleeping = false;
        public Container pants = new("blue jeans", 0.0f, 0.0f);
        public Container underwear = new("dinosaur undies", 0.0f, 0.0f);


        //Change current bladder value and handle warning messages
        public void AddBladder(float amount)
        {
            //If Wetting is disabled, don't do anything
            if (!Regression.config.Wetting)
                return;

            //Increment the current amount
            //We allow bladder to go over-full, to simulate the possibility of multiple night wettings
            //This is determined by the amount of water you have in your system when you go to bed
            float oldFullness = bladderFullness / maxBladderCapacity;
            bladderFullness += amount;

            //Did we go over? Then have an accident.
            if (bladderFullness >= bladderCapacity)
            {
                Wet(voluntary: false, inUnderwear: true);
                //Otherwise, calculate the new value
            } else
            {
                float newFullness = bladderFullness / maxBladderCapacity;
                //If we have no room left, or randomly based on our current continence level warn about how badly we need to pee
                if ((newFullness <= 0.0 ? 1.0 : bladderContinence / (4f * newFullness)) > Regression.rnd.NextDouble())
                {
                    Warn(oldFullness, newFullness, WETTING_THRESHOLDS, WETTING_MESSAGES, false);
                }
            }
        }

        //Change current bowels value and handle warning messages
        public void AddBowel(float amount)
        {
            //If Wetting is disabled, don't do anything
            if (!Regression.config.Messing)
                return;

            //Increment the current amount
            //We allow bowels to go over-full, to simulate the possibility of multiple night messes
            //This is determined by the amount of ffod you have in your system when you go to bed
            float oldFullness = bowelFullness / maxBowelCapacity;
            bowelFullness += amount;

            //Did we go over? Then have an accident.
            if (bowelFullness >= bowelCapacity)
            {
                Mess(voluntary: false, inUnderwear: true);
            }
            else
            {
                float newFullness = bowelFullness / maxBowelCapacity;
                //If we have no room left, or randomly based on our current continence level warn about how badly we need to pee
                if ((newFullness <= 0.0 ? 1.0 : bowelContinence / (4f * newFullness)) > Regression.rnd.NextDouble())
                {
                    Warn(oldFullness, newFullness, MESSING_THRESHOLDS, MESSING_MESSAGES, false);
                }
            }
        }

        //Change current Food value and handle warning messages
        //Notice that we do things here even if Hunger and Thirst are disabled
        //This is due to Food and Water's effect on Wetting/Messing
        public void AddFood(float amount, float conversionRatio = 0.5f)
        {
            //How full are we?
            float oldPercent = (requiredCaloriesPerDay - hunger) / requiredCaloriesPerDay;
            hunger -= amount;
            float newPercent = (requiredCaloriesPerDay - hunger) / requiredCaloriesPerDay;

            //Convert food lost into poo at half rate
            AddBowel(amount * conversionRatio);

            //If we go over full, add additional to bowels at half rate
            if (hunger < 0)
            {
                AddBowel(hunger * -1f * conversionRatio);
                hunger = 0f;
            }

            if (Regression.config.NoHungerAndThirst)
                return;

            //If we're starving and not eating, take a stamina hit
            if (hunger > requiredCaloriesPerDay && amount < 0)
            {
                //Take percentage off stamina equal to precentage above max hunger
                Game1.player.stamina += newPercent * Game1.player.MaxStamina;
                hunger = requiredCaloriesPerDay;
            }

            Warn(oldPercent, newPercent, HUNGER_THRESHOLDS, HUNGER_MESSAGES, false);
        }

        public void AddWater(float amount, float conversionRatio = 0.5f)
        {
            //How full are we?
            float oldPercent = (requiredWaterPerDay - thirst) / requiredWaterPerDay;
            thirst -= amount;
            float newPercent = (requiredWaterPerDay - thirst) / requiredWaterPerDay;

            //Convert water lost into pee at half rate
            AddBladder(amount * conversionRatio);

            //Also if we go over full, add additional to Bladder at half rate
            if (thirst < 0)
            {
                AddBladder((thirst * -1f * conversionRatio);
                thirst = 0f;
            }

            if (Regression.config.NoHungerAndThirst)
                return;

            //If we're starving and not eating, take a stamina hit
            if (thirst > requiredWaterPerDay && amount < 0)
            {
                //Take percentage off health equal to precentage above max thirst
                float lostHealth = newPercent * (float)Game1.player.maxHealth;
                Game1.player.health = Game1.player.health + (int)lostHealth;
                thirst = requiredWaterPerDay;
            }

            Warn(oldPercent, newPercent, THIRST_THRESHOLDS, THIRST_MESSAGES, false);
        }

        //Apply changes to the Maximum capacity of the bladder, and the rate at which it fills.
        public void ChangeBladderContinence(float percent = 0.01f)
        {
            float previousContinence = bladderContinence;

            //Modify the continence factor (inversly proportional to rate at which the bladder fills)
            bladderContinence -= percent;

            //Put a ceilling at 100%, and  a floor at 5%
            bladderContinence = Math.Max(Math.Min(bladderContinence, 1f), 0.05f);

            //Decrease our maximum capacity (bladder shrinks as we become incontinent)
            bladderCapacity = bladderContinence * maxBladderCapacity;

            //Ceilling at base value and floor at 25% base value
            bladderCapacity = Math.Max(bladderCapacity, minBladderCapacity);

            //If we're increasing, no need to warn. (maybe we should tell people that they're regaining?)
            if (percent >= 0)
                return;

            //Warn that we may be losing control
            Warn(previousContinence, bladderContinence, BLADDER_CONTINENCE_THRESHOLDS, BLADDER_CONTINENCE_MESSAGES, true);
        }

        //Apply changes to the Maximum capacity of the bowels, and the rate at which they fill.
        public void ChangeBowelContinence(float percent = 0.01f)
        {
            float previousContinence = bowelContinence;

            //Modify the continence factor (inversly proportional to rate at which the bowels fills)
            bowelContinence -= percent;

            //Put a ceilling at 100%, and  a floor at 5%
            bowelContinence = Math.Max(Math.Min(bowelContinence, 1f), 0.05f);

            //Decrease our maximum capacity (bowel shrinks as we become incontinent)
            bowelCapacity = bowelContinence * maxBowelCapacity;

            //Ceilling at base value and floor at 25% base value
            bowelCapacity = Math.Max(bowelCapacity, minBowelCapacity);

            //If we're increasing, no need to warn. (maybe we should tell people that they're regaining?)
            if (percent >= 0)
                return;

            //Warn that we may be losing control
            Warn(previousContinence, bowelContinence, BOWEL_CONTINENCE_THRESHOLDS, BOWEL_CONTINENCE_MESSAGES, true);
        }

        //Put on underwear and clean pants
        private Container ChangeUnderwear(Container container)
        {
            Container underwear = this.underwear;
            this.underwear = container;
            pants = new Container("blue jeans", 0.0f, 0.0f);
            CleanPants();
            Animations.Say(Regression.t.Change, this);
            return underwear;
        }

        public Container ChangeUnderwear(Underwear uw)
        {
            return ChangeUnderwear(new Container(uw.container.name, uw.container.wetness, uw.container.messiness));
        }

        public Container ChangeUnderwear(string type)
        {
            return ChangeUnderwear(new Container(type, 0.0f, 0.0f));
        }

        //If we put on our pants, remove wet/messy debuffs
        public void CleanPants()
        {
            RemoveBuff(WET_DEBUFF);
            RemoveBuff(MESSY_DEBUFF);
        }

        //Debug Function, Add a bit of everything
        public void DecreaseEverything()
        {
            AddWater(requiredWaterPerDay * -0.1f, 0f);
            AddFood(requiredCaloriesPerDay * -0.1f, 0f);
            AddBladder(maxBladderCapacity * 0.1f);
            AddBowel(maxBladderCapacity * 0.1f);
        }

        public void IncreaseEverything()
        {
            AddWater(requiredWaterPerDay * 0.1f, 0f);
            AddFood(requiredCaloriesPerDay * 0.1f, 0f);
            AddBladder(maxBladderCapacity * -0.1f);
            AddBowel(maxBladderCapacity * -0.1f);
        }

        public void DrinkWateringCan()
        {
            Farmer player = Game1.player;
            WateringCan currentTool = (WateringCan)player.CurrentTool;
            if (currentTool.WaterLeft * 100 >= thirst)
            {
                this.AddWater(thirst);
                currentTool.WaterLeft -= (int)(thirst / 100f);
                Animations.AnimateDrinking(false);
            }
            else if (currentTool.WaterLeft > 0)
            {
                this.AddWater(currentTool.WaterLeft * 100);
                currentTool.WaterLeft = 0;
                Animations.AnimateDrinking(false);
            }
            else
            {
                player.doEmote(4);
                Game1.showRedMessage("Out of water");
            }
        }

        public void DrinkWaterSource()
        {
            this.AddWater(thirst);
            Animations.AnimateDrinking(true);
        }

        public bool InToilet(bool inUnderwear)
        {
            return !inUnderwear && (Game1.currentLocation is FarmHouse);
        }

        public void Mess(bool voluntary = false, bool inUnderwear = true)
        {
            float amount = (float)((double)this.maxBowels * (double)hours * 20.0);
            this.bowels -= amount;
            if (this.sleeping)
            {
                this.messingVoluntarily = Regression.rnd.NextDouble() < (double)this.bowelContinence;
                if (this.messingVoluntarily)
                {
                    ++this.poopedToiletLastNight;
                }
                else
                {
                    double num = (double)this.pants.AddPoop(this.underwear.AddPoop(amount));
                }
            }
            else if (this.messingUnderwear)
            {
                double num1 = (double)this.pants.AddPoop(this.underwear.AddPoop(amount));
            }
            if ((double)this.bowels > 0.0)
                return;
            this.bowels = 0.0f;
            this.EndMessing();
        }

        public void StartMessing(bool voluntary = false, bool inUnderwear = true)
        {
            if (!Regression.config.Messing)
                return;

            if (bowelFullness < bowelAttemptThreshold)
            {
                Animations.AnimatePoopAttempt(this, inUnderwear);
            }
            else
            {
                if (!voluntary || bowelFullness > bowelTrainingThreshold)
                    this.ChangeBowelContinence(-0.01f);
                else
                    this.ChangeBowelContinence(0.01f);

                Animations.AnimateMessingStart(this, voluntary, inUnderwear);
            }
        }

        public void EndMessing()
        {
            Animations.AnimateMessingEnd();
            if (isSleeping || (Animations.HandleVillager(this, true, messingUnderwear, pants.messiness > 0.0, false, 20, 3) || pants.messiness <= 0.0 || !messingUnderwear))
                return;
            HandlePoopOverflow(pants);
        }

        public void StartWetting(bool voluntary = false, bool inUnderwear = true)
        {
            if (!Regression.config.Wetting)
                return;


            if ((double)bladderFullness < bladderAttemptThreshold)
            {
                Animations.AnimatePeeAttempt(this, inUnderwear, Game1.currentLocation is FarmHouse);
            }
            else
            {
                if (!voluntary || bladderFullness < bladderTrainingThreshold)
                    this.ChangeBladderContinence(-0.01f);
                else
                    this.ChangeBladderContinence(0.01f);
                Animations.AnimateWettingStart(this, voluntary, inUnderwear);
            }
        }

        public void Wet(bool voluntary = false, bool inUnderwear = true)
        {
            //If we're sleeping check if we have an accident or get up to use the potty
            if (isSleeping)
            {
                //When we're sleeping, our bladder fullness can exceed our capacity since we calculate for the whole night at once
                //Hehehe, this may be evil, but with a smaller bladder, you'll have to pee multiple times a night
                //So roll the dice each time >:)
                int numWettings = (int)(bladderFullness / bladderCapacity);
                float additionalAmount = bladderFullness - (numWettings * bladderCapacity);
                bool noWettings = true;

                if (additionalAmount > 0)
                    numWettings++;

                for(int i = 0; i < numWettings; i++)
                {
                    //Randomly decide if we get up. Less likely if we have lower continence
                    bool lclVoluntary = voluntary || Regression.rnd.NextDouble() < (double)this.bladderContinence;
                    if (!lclVoluntary)
                    {
                        noWettings = false;
                        //Any overage in the container, add to the pants. Ignore overage over that.
                        //When sleeping, the pants are actually the bed
                        if(i != numWettings-1)
                          _ = this.pants.AddPee(this.underwear.AddPee(bladderCapacity));
                        else
                          _ = this.pants.AddPee(this.underwear.AddPee(additionalAmount));

                    }
                }
            }
            else if (inUnderwear)
            {
                //Any overage in the container, add to the pants. Ignore overage over that.
                _ = this.pants.AddPee(this.underwear.AddPee(bladderCapacity));
            }
            if (bladder > 0.0)
                return;
            this.bladder = 0.0f;
            this.EndWetting();
        }
        public void EndWetting()
        {
            this.isWetting = false;
            Animations.AnimateWettingEnd(this);
            if (sleeping || (Animations.HandleVillager(this, false, wettingUnderwear, pants.wetness > 0.0, false, 20, 3) || pants.wetness <= 0.0 || !wettingUnderwear))
                return;
            HandlePeeOverflow(pants);
        }

        public void HandleMorning()
        {
            if (Regression.config.Easymode)
            {
                food = maxFood;
                water = maxWater;
            }
            if (!Regression.config.Wetting && !Regression.config.Messing)
            {
                peedToiletLastNight = 0;
                poopedToiletLastNight = 0;
                sleeping = false;
                pants = new Container("blue jeans", 0.0f, 0.0f);
            }
            else
            {
                if (!Regression.config.Easymode)
                {
                    int num = new Random().Next(1, 13);
                    if (num <= 2 && (pants.messiness > 0.0 || pants.wetness > (double)glassOfWater))
                    {
                        beddingDryTime = Game1.timeOfDay + 1000;
                        Farmer player = Game1.player;
                        player.stamina = (player.stamina - 20f);
                    }
                    else if (num <= 5 && pants.wetness > 0.0)
                    {
                        beddingDryTime = Game1.timeOfDay + 600;
                        Farmer player = Game1.player;
                        player.stamina = (player.stamina - 10f);
                    }
                    else
                        beddingDryTime = 0;
                }
                Animations.AnimateMorning(this);
                peedToiletLastNight = 0;
                poopedToiletLastNight = 0;
                sleeping = false;
                pants = new Container("blue jeans", 0.0f, 0.0f);
            }
        }

        public void HandleNight()
        {
            lastStamina = Game1.player.stamina;
            pants = new Container("bed", 0.0f, 0.0f);
            sleeping = true;
            if (bedtime <= 0)
                return;

            //How long are we sleeping? (Minimum of 4 hours)
            const int timeInDay = 2400;
            const int wakeUpTime = timeInDay + 600;
            const float sleepRate = 3.0f; //Let's say body functins change @ 1/3 speed while sleeping. Arbitrary.
            int timeSlept = wakeUpTime - bedtime; //Bedtime will never exceed passout-time of 2:00AM (2600) 
            HandleTime(timeSlept / 100.0f / sleepRate);
        }

        public void HandlePeeOverflow(Container pants)
        {
            Animations.Write(Regression.t.Pee_Overflow, this);
            int num = -Math.Max(Math.Min((int)(pants.wetness / pants.absorbency * 10.0), 10), 1);
            Buff buff = new Buff(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, num, 0, 15, "", "")
            {
                description = string.Format("{0} {1} Defense.", Strings.RandString(Regression.t.Debuff_Wet_Pants), num),
                millisecondsDuration = 1080000
            };
            buff.glow = pants.messiness != 0.0 ? Color.Brown : Color.Yellow;
            buff.sheetIndex = -1;
            buff.which = WET_DEBUFF;
            if (Game1.buffsDisplay.hasBuff(WET_DEBUFF))
                this.RemoveBuff(WET_DEBUFF);
            Game1.buffsDisplay.addOtherBuff(buff);
        }

        public void HandlePoopOverflow(Container pants)
        {
            Animations.Write(Regression.t.Poop_Overflow, this);
            float num1 = pants.messiness / pants.containment;
            int num2 = num1 >= 0.5 ? (num1 > 1.0 ? -3 : -2) : -1;
            Buff buff = new Buff(0, 0, 0, 0, 0, 0, 0, 0, 0, num2, 0, 0, 15, "", "")
            {
                description = string.Format("{0} {1} Speed.", Strings.RandString(Regression.t.Debuff_Messy_Pants), (object)num2),
                millisecondsDuration = 1080000,
                glow = Color.Brown,
                sheetIndex = -1,
                which = MESSY_DEBUFF
            };
            if (Game1.buffsDisplay.hasBuff(MESSY_DEBUFF))
                this.RemoveBuff(MESSY_DEBUFF);
            Game1.buffsDisplay.addOtherBuff(buff);
        }

        public void HandleStamina()
        {
            float num = (float)((Game1.player.stamina - (double)this.lastStamina) / 4.0);
            if ((double)num == 0.0)
                return;
            if (num < 0.0)
            {
                this.AddFood(num / 300f * this.maxFood);
                this.AddWater(num / 100f * this.maxWater, 0.05f);
            }
            this.lastStamina = Game1.player.stamina;
        }

        public void HandleStomach(float hours)
        {
            float lostHunger = this.foodDay * hours;
            float lostHydration = Body.glassOfWater * 2f * hours;
            float actualLostHunger = Math.Min(this.stomach[HUNGER], lostHunger);
            float actualLostHydration = Math.Min(this.stomach[THIRST], lostHydration);

            //Convert body functions to waste products
            this.AddBowel(actualLostHunger); //Hunger decrease = bowel increase
            this.AddBladder(actualLostHydration); //Hydration decrease = Bladder increase
            this.AddStomach(-actualLostHunger, -actualLostHydration);
        }

        public void HandleTime(float hours)
        {
            this.HandleStamina();
            this.AddWater((float)(requiredWaterPerDay * (double)hours / -24.0));
            this.AddFood((float)(requiredCaloriesPerDay * (double)hours / -24.0));
            this.HandleStomach(hours);
            if (this.isWetting)
                this.Wet(hours);
            if (!this.isMessing)
                return;
            this.Mess(hours);
        }

        public bool IsFishing()
        {
            FishingRod currentTool;
            return (currentTool = Game1.player.CurrentTool as FishingRod) != null && (currentTool.isCasting || currentTool.isTimingCast || (currentTool.isNibbling || currentTool.isReeling) || currentTool.castedButBobberStillInAir || currentTool.pullingOutOfWater);
        }


        public void RemoveBuff(int which)
        {
            BuffsDisplay buffsDisplay = Game1.buffsDisplay;
            for (int index = buffsDisplay.otherBuffs.Count - 1; index >= 0; --index)
            {
                if (buffsDisplay.otherBuffs[index].which == which)
                {
                    buffsDisplay.otherBuffs[index].removeBuff();
                    buffsDisplay.otherBuffs.RemoveAt(index);
                    buffsDisplay.syncIcons();
                }
            }
        }

        public void Warn(float oldPercent, float newPercent, float[] thresholds, string[][] msgs, bool write = false)
        {
            if (isSleeping)
                return;
            for (int index = 0; index < thresholds.Length; ++index)
            {
                if ((double)oldPercent > (double)thresholds[index] && (double)newPercent <= (double)thresholds[index])
                {
                    if (write)
                    {
                        Animations.Write(msgs[index], this);
                        break;
                    }
                    Animations.Warn(msgs[index], this);
                    break;
                }
            }
        }


        //Are we available to Wet/Mess
        public bool IsOccupied()
        {
            return isWetting || isMessing || IsFishing();
        }
    }
}
