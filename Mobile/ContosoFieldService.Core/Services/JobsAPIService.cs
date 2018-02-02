﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ContosoFieldService.Models;
using MonkeyCache.LiteDB;
using Plugin.Connectivity;
using Polly;
using Refit;

namespace ContosoFieldService.Services
{
    [Headers(Helpers.Constants.ApiManagementKey)]
    public interface IJobServiceAPI
    {
        [Get("/job/")]
        Task<List<Job>> GetJobs();

        [Get("/job/{id}/")]
        Task<Job> GetJobById(string id);

        [Get("/search/jobs/?keyword={keyword}")]
        Task<List<Job>> SearchJobs(string keyword);

        [Post("/job/")]
        Task<Job> CreateJob([Body] Job job);

        [Delete("/job/{id}/")]
        Task<Job> DeleteJob(string id);

        [Put("/job/{id}/")]
        Task<Job> UpdateJob(string id, [Body] Job job);
    }

    public class JobsAPIService
    {
        public async Task<Job> CreateJobAsync(Job job)
        {
            var contosoMaintenanceApi = RestService.For<IJobServiceAPI>(Helpers.Constants.BaseUrl);
            return await contosoMaintenanceApi.CreateJob(job);
        }

        public async Task<List<Job>> GetJobsAsync()
        {
            var key = "Jobs";

            //Handle online/offline scenario
            if (!CrossConnectivity.Current.IsConnected && Barrel.Current.Exists(key))
            {
                //If no connectivity, we'll return the cached jobs list.
                return Barrel.Current.Get<List<Job>>(key);
            }

            //If the data isn't too old, we'll go ahead and return it rather than call the backend again.
            if (!Barrel.Current.IsExpired(key) && Barrel.Current.Exists(key))
            {
                return Barrel.Current.Get<List<Job>>(key);
            }            

            //Create an instance of the Refit RestService for the job interface.
            var contosoMaintenanceApi = RestService.For<IJobServiceAPI>(Helpers.Constants.BaseUrl);

            //Use Polly to handle retrying (helps with bad connectivity) 
            var jobs = await Policy
                .Handle<WebException>()
                .Or<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync
                (
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                ).ExecuteAsync(async () => await contosoMaintenanceApi.GetJobs());

            //Save jobs into the cache
            Barrel.Current.Add(key: key, data: jobs, expireIn: TimeSpan.FromSeconds(5));
            return jobs;
        }

        public async Task<Job> GetJobByIdAsync(string id)
        {
            var contosoMaintenanceApi = RestService.For<IJobServiceAPI>(Helpers.Constants.BaseUrl);
            return await contosoMaintenanceApi.GetJobById(id);
        }

        public async Task<Job> DeleteJobByIdAsync(string id)
        {
            var contosoMaintenanceApi = RestService.For<IJobServiceAPI>(Helpers.Constants.BaseUrl);
            return await contosoMaintenanceApi.DeleteJob(id);
        }

        public async Task<Job> UpdateJob(Job job)
        {
            var contosoMaintenanceApi = RestService.For<IJobServiceAPI>(Helpers.Constants.BaseUrl);
            return await contosoMaintenanceApi.UpdateJob(job.Id, job);
        }

        public async Task<List<Job>> SearchJobsAsync(string keyword)
        {
            var contosoMaintenanceApi = RestService.For<IJobServiceAPI>(Helpers.Constants.BaseUrl);
            return await contosoMaintenanceApi.SearchJobs(keyword);
        }
    }
}
