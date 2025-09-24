# Football AI - Reinforcement Learning Implementation

Detta projekt implementerar en Reinforcement Learning-baserad AI för fotbollssimulering som lär sig genom self-play.

## Översikt

Systemet innehåller:
- **ReinforcementLearningAI**: Neural network som lär sig genom erfarenhet
- **RLPlayerService**: Service som använder RL-AI för spelarbeslut
- **PlayerService**: Original heuristikbaserad AI för jämförelse
- **Self-play träning**: Två AI:er spelar mot varandra och förbättras

## Snabbstart

### 1. Starta Training Setup
```bash
./start_training.sh
```

Detta startar tre AI-instanser:
- **Port 5001**: RL AI Team 1 
- **Port 5002**: RL AI Team 2
- **Port 5003**: Heuristic AI (för jämförelse)

### 2. Konfigurera Simulatorn
I fotbollssimulatorn, registrera nya lag med följande URLs:
- Team 1: `http://localhost:5001`
- Team 2: `http://localhost:5002`

### 3. Starta Matcher
Låt AI:erna spela mot varandra. De kommer att:
- Lära sig från varje match
- Förbättra sina strategier över tid
- Logga träningsframsteg i konsolen

## AI-Arkitektur

### Neural Network
- **Input**: 40 features (spelarpositioner, hastigheter, bollstatus, match-info)
- **Hidden**: 256 → 128 neurons med ReLU activation
- **Output**: 24 actions (6 per spelare: rörelse + bollhantering)

### Reward System
AI:n belönas för:
- ✅ Mål (100 poäng)
- ✅ Bollkontroll (1-2 poäng) 
- ✅ Framåtspel (0.5-1 poäng)
- ✅ Bra positionering (0.2-0.5 poäng)
- ❌ Insläppta mål (-100 poäng)
- ❌ Dålig formation (-0.1 poäng)

### Exploration vs Exploitation
- Börjar med 10% random actions (exploration)
- Minskar gradvis till 1% (exploitation)
- Lägger till små mängder brus för kontinuerlig exploration

## Konfiguration

Ändra AI-inställningar i `appsettings.Development.json`:

```json
{
  "AISettings": {
    "AIType": "ReinforcementLearning",  // eller "Heuristic"
    "EnableTraining": true,
    "EnableLogging": true,
    "ExplorationRate": 0.1
  }
}
```

Eller använd environment variables:
```bash
AISettings__AIType="Heuristic" dotnet run --urls=http://localhost:5003
```

## Modellsparning & Persistens

### Automatisk Sparning
- **Sparas efter varje 10:e match** eller **var 5:e minut**
- **Separata modeller** för varje instans:
  - `models/rl_model_instance_1.json` (Team 1)
  - `models/rl_model_instance_2.json` (Team 2)
  - `models/rl_model_instance_3.json` (Heuristic)

### Vad Sparas
- Neural network vikter och bias
- Träningsstatistik (totala spel, vinster, förluster)
- Exploration rate (minskar över tid)
- Tidsstämpel för senaste sparning

### Kontinuerlig Träning
- När du startar om, **fortsätter AI:erna från där de slutade**
- Ingen träning förloras vid omstart
- Perfekt för långtidstträning över dagar/veckor

## Övervakningsloggar

AI:n loggar träningsframsteg var 100:e steg:
```
Instance 1 - Step 1000: Avg reward: 2.35, Buffer size: 1000, Exploration: 0.085, Win rate: 45.00%
Instance 1 - Game ended. Score: 2-1, Avg reward: 1.89, Win rate: 46.67%, Exploration: 0.084
Instance 1 - Model saved: 47/100 wins
```

## Förväntad Utveckling

### Vecka 1
- Random spel, lär sig grundregler
- Win rate: ~50% (slumpmässigt)

### Vecka 2-4  
- Lär sig jaga bollen och skjuta
- Win rate vs heuristik: ~30-40%

### Månad 2-3
- Utvecklar avancerade strategier
- Win rate vs heuristik: ~70-80%

### Månad 6+
- Potentiellt övermänskliga strategier
- Win rate: >90%

## Teknisk Implementation

### Key Features
- **Real-time learning**: Tränar under spelets gång
- **Experience replay**: Lagrar och återanvänder tidigare erfarenheter
- **Epsilon-greedy exploration**: Balanserar utforskning vs exploitation
- **Multi-instance support**: Kör flera AI:er simultant
- **Fallback safety**: Använder enkel heuristik om AI:n fallerar

### Fil Struktur
```
Domain/
├── AI/
│   ├── IFootballAI.cs              # AI interface
│   ├── ReinforcementLearningAI.cs  # Huvud RL implementation
│   ├── SimpleNeuralNetwork.cs      # Neural network från scratch
│   ├── GameStateEncoder.cs         # Konverterar game state till vectors
│   └── RewardCalculator.cs         # Belöningssystem
├── RLPlayerService.cs              # RL-baserad PlayerService
└── PlayerService.cs                # Original heuristik
```

## Troubleshooting

### AI:n gör konstiga drag
- Normalt under första dagarna (exploration phase)
- Kontrollera exploration rate i loggar
- Vänta tills den lärt sig grunderna

### Träning går långsamt
- Öka antal matcher per dag
- Kontrollera att båda AI:erna tränar aktivt
- Se till att experience buffer fylls på (visas i loggar)

### Portar upptagna
```bash
# Kolla vilka processer som använder portarna
lsof -i :5001
lsof -i :5002
lsof -i :5003

# Döda processer om nödvändigt
kill -9 <PID>
```

## Nästa Steg

1. **Förbättra Neural Network**: Lägg till convolutional layers för spatial awareness
2. **Implement Model Saving**: Spara och ladda tränade modeller
3. **Advanced Rewards**: Mer sofistikerade belöningsfunktioner
4. **Population Training**: Träna flera modeller samtidigt
5. **Opponent Modeling**: Lär sig motståndares beteenden

## Bidrag

För att förbättra AI:n:
1. Experimentera med olika reward functions i `RewardCalculator.cs`
2. Justera neural network arkitekturen i `SimpleNeuralNetwork.cs`
3. Ändra exploration strategier i `ReinforcementLearningAI.cs`

---

**Lycka till med träningen! 🤖⚽**