using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace CheckIn
{
    public class FaceDetection
    {
        FaceServiceClient faceServiceClient;
        const string personGroupId = "finsify";

        public FaceDetection()
        {
            faceServiceClient = new FaceServiceClient("8b2d348fca9e44b7be74e4cdabf99973", "https://southeastasia.api.cognitive.microsoft.com/face/v1.0");
        }

        public async Task CreateRootGroup()
        {
            try
            {
                await faceServiceClient.CreatePersonGroupAsync(personGroupId, "Finsify");
            }
            catch { }
        }

        public async Task CreatePersonGroup(string name, StorageFolder folder)
        {
            try
            {
                var friend = await faceServiceClient.CreatePersonAsync(
                   personGroupId,
                   name
                );

                foreach (string imagePath in Directory.GetFiles(folder.Path, "*.jpg"))
                {
                    using (Stream s = File.OpenRead(imagePath))
                    {
                        await faceServiceClient.AddPersonFaceAsync(
                            personGroupId, friend.PersonId, s);
                    }
                }
            }
            catch (FaceAPIException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public async Task TrainGroup()
        {
            await faceServiceClient.TrainPersonGroupAsync(personGroupId);

            TrainingStatus trainingStatus = null;
            while (true)
            {
                trainingStatus = await faceServiceClient.GetPersonGroupTrainingStatusAsync(personGroupId);

                if (trainingStatus.Status != Status.Running)
                    break;

                await Task.Delay(1000);
            }
        }

        private static bool isRuning;
        public async Task<IdentifyResult[]> IdentifyAsync(string file)
        {
            if (isRuning) return null;
            isRuning = true;
            try
            {
                using (Stream s = File.OpenRead(file))
                {
                    var faces = await faceServiceClient.DetectAsync(s);
                    var faceIds = faces.Select(face => face.FaceId).ToArray();

                    return await faceServiceClient.IdentifyAsync(personGroupId, faceIds);
                    //foreach (var identifyResult in results)
                    //{
                    //    Debug.WriteLine("Result of face: {0}", identifyResult.FaceId);
                    //    if (identifyResult.Candidates.Length == 0)
                    //    {
                    //        Debug.WriteLine("No one identified");
                    //    }
                    //    else
                    //    {
                    //        // Get top 1 among all candidates returned
                    //        var candidateId = identifyResult.Candidates[0].PersonId;
                    //        var person = await faceServiceClient.GetPersonAsync(personGroupId, candidateId);

                    //        var text = $"Identified as {person.Name}";
                    //        Debug.WriteLine(text);
                    //        DisplayUtils.ShowToast(text);

                    //    }
                    //}
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
            isRuning = false;
            return null;
        }

        public Task<Person> GetPersonName(Guid candidateId)
        {
            return faceServiceClient.GetPersonAsync(personGroupId, candidateId);
        }

    }
}
