# Thaheen – ذهين

Thaheen is a Saudi-inspired educational financial simulation game built in Unity.

The game teaches children and young adults how to make better financial decisions using virtual money inside an interactive financial city.

Players learn about:

- Saving
- Investing
- Smart spending
- Increasing income
- Charity
- Financial goal planning

Thaheen is not a banking application.

It is a game-based financial learning experience with an Arabic RTL interface and a Saudi-inspired visual identity.

## Main Features

- Arabic-first RTL experience
- Player onboarding flow
- Custom player name
- Custom financial goal
- Custom target amount
- Monthly starting money selected by the player
- Starting money range from 50 to 1000 SAR
- Interactive isometric financial city
- Four clickable financial buildings
- Saving system
- Shopping and income activities
- Saudi company investment simulation
- Charity system
- AI financial behavior analysis
- Personalized Arabic financial report
- Goal progress tracking
- Virtual money only

## Game Scenes

The project currently uses two main scenes:

1. `StartFlowScene`
2. `OGscene`

The game must start from `StartFlowScene`.

### StartFlowScene

This is the onboarding scene.

The player:

1. Presses `ابدأ المغامرة`
2. Enters their name
3. Enters their financial goal
4. Enters the target amount
5. Selects their monthly starting money

The monthly starting money must be between:

- Minimum: 50 SAR
- Maximum: 1000 SAR

The selected monthly amount becomes the player's initial available wallet balance.

After completing the onboarding flow, the game automatically loads `OGscene`.

### OGscene

This is the main playable financial city.

The city uses a Hay Day-style isometric camera.

There is:

- No walking character
- No joystick
- No free-roaming player avatar

The player interacts directly with the city by clicking buildings.

Camera controls:

- Drag to move around the city
- Zoom in and out
- Click buildings to open their interfaces

## Main Buildings

Only four main buildings exist in the city.

### Investment Tower

Scene object:

`AI_Alinma_Investment_Tower`

The investment tower allows the player to:

- View Saudi companies
- View company details
- Choose the number of shares
- Buy shares
- Sell owned shares
- View the portfolio
- View transaction history
- Review investment performance

The investment companies include examples such as:

- Alinma
- Saudi Aramco
- STC

Successful investment purchases reduce the available wallet balance.

Selling shares returns the sale value to the wallet.

### Shopping Mall

Scene object:

`Burj_AlmammIkah_mall`

The mall allows the player to:

- Spend virtual money
- Complete income activities
- Earn additional virtual money

Successful purchases reduce the wallet balance.

Rewards and income activities increase the wallet balance.

### Charity House

Scene object:

`charity_house`

The charity house allows the player to:

- Choose a donation amount
- Donate virtual money
- Read educational messages about generosity

Successful donations reduce the wallet balance.

### Najdi Piggy Bank House

Scene object:

`hassalah_najdi_house`

The Najdi house represents the saving system.

The player can:

- Choose an amount to save
- Transfer money from the available wallet into savings
- Track progress toward the financial goal

Saving does not create new money.

The saved amount is transferred from the available wallet balance.

## City HUD

The main city HUD displays:

- Player name
- Available wallet balance
- Goal progress
- Settings button
- AI Financial Report button
- Exit button

The HUD updates after successful financial actions.

## AI Financial Report

Thaheen connects to a deployed FastAPI backend for financial behavior analysis.

API endpoint:

`https://dhaheen-ai-analyzer.onrender.com/analyze`

The AI report analyzes supported player decisions such as:

- Saving
- Shopping
- Investing

The report may display:

- Financial personality
- Overall score
- Saving score
- Spending-control score
- Investment score
- Emergency-readiness score
- Arabic report summary
- Financial tip
- Personalized recommendations

An internet connection is required to generate the AI report.

The first request may take longer because the Render service may need to wake up.

No API key is stored inside Unity.

## How to Play

1. Start the game from `StartFlowScene`.
2. Press `ابدأ المغامرة`.
3. Enter the player name.
4. Enter a financial goal.
5. Enter the target amount.
6. Select monthly starting money between 50 and 1000 SAR.
7. Continue to the city.
8. Drag and zoom around the city.
9. Click one of the four buildings.
10. Complete financial actions.
11. Close a popup using `إغلاق`.
12. Complete at least one supported financial action.
13. Click the AI Financial Report button.
14. Wait for the Arabic analysis result.

## How to Run the Project in Unity

1. Open the Thaheen project in Unity.
2. Open `StartFlowScene`.
3. Press Play.
4. Complete the onboarding flow.
5. The game should automatically load `OGscene`.
6. Test the buildings and financial systems.
7. Open the AI Financial Report after completing at least one action.

## Build Scene Order

The Unity Build Profiles scene order must be:

1. `StartFlowScene`
2. `OGscene`

`SampleScene` should remain disabled or be removed from the active build scene list.

## How to Build for Windows

1. Open `File → Build Profiles`.
2. Select `Windows`.
3. Open `Scene List`.
4. Confirm that `StartFlowScene` is first.
5. Confirm that `OGscene` is second.
6. Click `Build` or `Build And Run`.
7. Select an empty output folder.

The Windows build folder must be distributed as a complete folder.

Do not share only the `.exe` file.

The build folder normally includes:

- The game `.exe`
- The `_Data` folder
- `UnityPlayer.dll`
- Other Unity-generated files

