﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace TwitchBot.Twitch;

public static class API {
	public async static Task<JsonObject> Request( string endpoint, HttpMethod? method = null, Dictionary<string, string>? queryParameters = null, JsonObject? payload = null ) {

		// Construct the URL
		string queryString = queryParameters != null ? $"?{queryParameters.ToQueryString()}" : "";
		Uri targetUrl = new( $"https://{Config.TwitchAPIBaseURL}/{endpoint}{queryString}" );

		// Create the request, defaulting to GET
		HttpRequestMessage httpRequest = new( method ?? HttpMethod.Get, targetUrl.ToString() );

		// Always expect a JSON response
		httpRequest.Headers.Accept.Add( MediaTypeWithQualityHeaderValue.Parse( "application/json" ) );

		// Add the OAuth credentials as headers
		httpRequest.Headers.Add( "Client-Id", Config.TwitchOAuthIdentifier );
		httpRequest.Headers.Authorization = Shared.UserAccessToken!.GetAuthorizationHeader();

		// Set the request body, if one is provided
		if ( payload != null ) httpRequest.Content = new StringContent( payload.ToJsonString(), Encoding.UTF8, "application/json" );

		// Send the request & make sure to retry when it fails due to token expiry
		HttpResponseMessage httpResponse = await Shared.httpClient.SendAsync( httpRequest );
		Log.Debug( "API request: {0} '{1}' '{2}' => {3} {4}", httpRequest.Method.ToString(), httpRequest.RequestUri?.ToString(), payload?.ToJsonString(), ( int ) httpResponse.StatusCode, httpResponse.StatusCode.ToString() );
		if ( httpResponse.StatusCode == HttpStatusCode.Unauthorized ) {
			Log.Warn( "User access token has expired, refreshing & saving..." );
			await Shared.UserAccessToken.DoRefresh();
			Shared.UserAccessToken.Save( Shared.UserAccessTokenFilePath );

			Log.Info( "Retrying API request: '{0}' '{1}'...", method, endpoint );
			return await Request( endpoint, method, queryParameters );
		}

		// We do not want to continue if the response is not successful
		httpResponse.EnsureSuccessStatusCode();

		// Read the response content as JSON
		System.IO.Stream responseStream = await httpResponse.Content.ReadAsStreamAsync();
		JsonNode? responseJson = JsonNode.Parse( responseStream );
		if ( responseJson == null ) throw new Exception( $"Failed to parse JSON response from API request: '{endpoint}'" );

		// Convert to a JSON object before returning
		return responseJson.AsObject();

	}
}
