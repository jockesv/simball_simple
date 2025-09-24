using System.Text.Json;

namespace FootballInstructor.Domain.AI
{
    public class ModelStorage
    {
        public float[,] WeightsInput { get; set; } = default!;
        public float[] BiasHidden1 { get; set; } = default!;
        public float[,] WeightsHidden1 { get; set; } = default!;
        public float[] BiasHidden2 { get; set; } = default!;
        public float[,] WeightsOutput { get; set; } = default!;
        public float[] BiasOutput { get; set; } = default!;
        
        // Training metadata
        public int TotalGames { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public float CurrentExplorationRate { get; set; }
        public DateTime LastSaved { get; set; }
        public string ModelVersion { get; set; } = "1.0";
    }

    public static class ModelSerializer
    {
        public static void SaveModel(SimpleNeuralNetwork network, string filePath, int totalGames, int wins, int losses, float explorationRate)
        {
            try
            {
                var storage = new ModelStorage
                {
                    WeightsInput = network.GetWeightsInput(),
                    BiasHidden1 = network.GetBiasHidden1(),
                    WeightsHidden1 = network.GetWeightsHidden1(),
                    BiasHidden2 = network.GetBiasHidden2(),
                    WeightsOutput = network.GetWeightsOutput(),
                    BiasOutput = network.GetBiasOutput(),
                    TotalGames = totalGames,
                    Wins = wins,
                    Losses = losses,
                    CurrentExplorationRate = explorationRate,
                    LastSaved = DateTime.Now
                };

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save as JSON for now (could be binary for performance)
                var json = JsonSerializer.Serialize(storage, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    // Custom converter for multidimensional arrays
                    Converters = { new MultiDimensionalArrayConverter() }
                });
                
                File.WriteAllText(filePath, json);
                Console.WriteLine($"Model saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving model: {ex.Message}");
            }
        }

        public static (SimpleNeuralNetwork?, int totalGames, int wins, int losses, float explorationRate) LoadModel(string filePath, int inputSize, int hiddenSize1, int hiddenSize2, int outputSize)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Model file not found: {filePath}");
                    return (null, 0, 0, 0, 0.1f);
                }

                var json = File.ReadAllText(filePath);
                var storage = JsonSerializer.Deserialize<ModelStorage>(json, new JsonSerializerOptions
                {
                    Converters = { new MultiDimensionalArrayConverter() }
                });

                if (storage == null)
                {
                    Console.WriteLine("Failed to deserialize model");
                    return (null, 0, 0, 0, 0.1f);
                }

                var network = new SimpleNeuralNetwork(inputSize, hiddenSize1, hiddenSize2, outputSize);
                network.SetWeights(
                    storage.WeightsInput,
                    storage.BiasHidden1,
                    storage.WeightsHidden1,
                    storage.BiasHidden2,
                    storage.WeightsOutput,
                    storage.BiasOutput
                );

                Console.WriteLine($"Model loaded from: {filePath}");
                Console.WriteLine($"Games: {storage.TotalGames}, Win rate: {(storage.TotalGames > 0 ? storage.Wins * 100.0 / storage.TotalGames : 0):F1}%");
                
                return (network, storage.TotalGames, storage.Wins, storage.Losses, storage.CurrentExplorationRate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading model: {ex.Message}");
                return (null, 0, 0, 0, 0.1f);
            }
        }
    }

    // Helper class for JSON serialization of multidimensional arrays
    public class MultiDimensionalArrayConverter : System.Text.Json.Serialization.JsonConverter<float[,]>
    {
        public override float[,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var jaggedArray = JsonSerializer.Deserialize<float[][]>(ref reader, options);
            if (jaggedArray == null) return new float[0, 0];
            
            var rows = jaggedArray.Length;
            var cols = rows > 0 ? jaggedArray[0].Length : 0;
            var result = new float[rows, cols];
            
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    result[i, j] = jaggedArray[i][j];
                    
            return result;
        }

        public override void Write(Utf8JsonWriter writer, float[,] value, JsonSerializerOptions options)
        {
            var rows = value.GetLength(0);
            var cols = value.GetLength(1);
            var jaggedArray = new float[rows][];
            
            for (int i = 0; i < rows; i++)
            {
                jaggedArray[i] = new float[cols];
                for (int j = 0; j < cols; j++)
                    jaggedArray[i][j] = value[i, j];
            }
            
            JsonSerializer.Serialize(writer, jaggedArray, options);
        }
    }
}