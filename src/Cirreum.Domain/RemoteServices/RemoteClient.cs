namespace Cirreum.RemoteServices;

using Cirreum.Exceptions;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Base class for API clients that provides common functionality for HTTP operations and
/// returns a <see cref="Result"/> or <see cref="Result{T}"/> for full railway programming.
/// </summary>
/// <remarks>
/// <para>
/// Can be used anywhere you would use <see cref="HttpClient"/> directly, with built-in
/// telemetry, error handling, and consistent Result-based returns.
/// </para>
/// <para>
/// Also works well with a command/query handler pattern where derived classes implement
/// methods that act as handlers against HTTP endpoints rather than repositories or services.
/// </para>
/// <para>
/// Retry support is available via <see cref="WithRetryAsync{T}"/> but is opt-in per operation.
/// </para>
/// <para>
/// For complete control, the underlying <see cref="Client"/> is accessible to derived classes.
/// </para>
/// </remarks>
public abstract class RemoteClient {


	/// <summary>
	/// The current <see cref="HttpClient"/> that is configured for this client.
	/// </summary>
	protected internal HttpClient Client { get; init; }
	/// <summary>
	/// The <see cref="ILogger"/> configured for this client.
	/// </summary>
	protected internal ILogger Logger { get; init; }
	/// <summary>
	/// Gets the current domain environment service.
	/// </summary>
	protected internal IDomainEnvironment DomainEnvironment { get; init; }
	/// <summary>
	/// Gets the optional <see cref="ActivitySource"/> for the application.
	/// </summary>
	protected internal ActivitySource? ActivitySource { get; init; }
	/// <summary>
	/// The configured or defaulted serialization options.
	/// </summary>
	protected JsonSerializerOptions JsonOptions { get; init; }

	/// <summary>
	/// The current UserAgent string for this client.
	/// </summary>
	protected string UserAgent { get; private set; } = string.Empty;

	/// <summary>
	/// The current instance's Type Name
	/// </summary>
	protected string TypeName { get; }

	/// <summary>
	/// DI Constructor.
	/// </summary>
	/// <param name="client">Injected HttpClient.</param>
	/// <param name="logger">Injected logger.</param>
	/// <param name="domainEnvironment">Injected <see cref="IDomainEnvironment"/>.</param>
	/// <param name="activitySource">The optional <see cref="ActivitySource"/></param>
	/// <param name="jsonOptions">Injected jsonOptions.</param>
	/// <remarks>
	/// <para>
	/// The default <see cref="JsonSerializerOptions"/> set the
	/// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> to true,
	/// the <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> to 
	/// <see cref="JsonNamingPolicy.CamelCase"/> and the
	/// the <see cref="JsonSerializerOptions.DefaultIgnoreCondition"/> to 
	/// <see cref="JsonIgnoreCondition.WhenWritingNull"/>.
	/// </para>
	/// </remarks>
	protected RemoteClient(
		HttpClient client,
		ILogger logger,
		IDomainEnvironment domainEnvironment,
		ActivitySource? activitySource,
		JsonSerializerOptions? jsonOptions = null) {
		this.TypeName = this.GetType().Name;
		this.Client = client;
		this.Logger = logger;
		this.DomainEnvironment = domainEnvironment;
		this.ActivitySource = activitySource;
		this.JsonOptions = jsonOptions ?? new JsonSerializerOptions {
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};
		this.SetUserAgent();
	}

	#region Helpers

	/// <summary>
	/// Set's the user agent header on the <see cref="Client"/>.
	/// </summary>
	protected virtual void SetUserAgent() {
		var clientVersion = this.GetType().GetTypeInfo().Assembly.GetName().Version?.ToString() ?? "v0";
		this.UserAgent = $"{this.TypeName}/{clientVersion}/{this.DomainEnvironment.RuntimeType}";
		this.Client.DefaultRequestHeaders.UserAgent.TryParseAdd(this.UserAgent);
	}

	/// <summary>
	/// Executes an operation with retry logic using exponential backoff.
	/// </summary>
	/// <typeparam name="T">The return type of the operation.</typeparam>
	/// <param name="operation">The operation to execute with retry logic.</param>
	/// <param name="maxAttempts">Maximum number of retry attempts.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The result of the operation, or throws the last exception if all retries fail.</returns>
	protected async Task<Result<T>> WithRetryAsync<T>(
		Func<CancellationToken, Task<Result<T>>> operation,
		int maxAttempts = 3,
		CancellationToken cancellationToken = default) {
		Exception? lastException = null;

		for (var attempt = 0; attempt < maxAttempts; attempt++) {
			try {
				var result = await operation(cancellationToken);
				if (result.IsSuccess) {
					return result;
				}
				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
				await Task.Delay(delay, cancellationToken);
			} catch (Exception ex) when (attempt < maxAttempts - 1 && this.IsTransient(ex)) {
				lastException = ex;
				if (this.Logger.IsEnabled(LogLevel.Warning)) {
					this.Logger.LogWarning(ex,
					"Attempt {Attempt} of {MaxAttempts} failed, retrying...",
					attempt + 1,
					maxAttempts);
				}

				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
				await Task.Delay(delay, cancellationToken);
			}
		}
		if (this.Logger.IsEnabled(LogLevel.Error)) {
			this.Logger.LogError(lastException,
			"All {MaxAttempts} retry attempts failed",
			maxAttempts);
		}
		throw lastException!;
	}

	/// <summary>
	/// Executes an operation with retry logic using exponential backoff.
	/// </summary>
	/// <param name="operation">The operation to execute with retry logic.</param>
	/// <param name="maxAttempts">Maximum number of retry attempts.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The result of the operation, or throws the last exception if all retries fail.</returns>
	protected async Task<Result> WithRetryAsync(
		Func<CancellationToken, Task<Result>> operation,
		int maxAttempts = 3,
		CancellationToken cancellationToken = default) {
		Exception? lastException = null;

		for (var attempt = 0; attempt < maxAttempts; attempt++) {
			try {
				var result = await operation(cancellationToken);
				if (result.IsSuccess) {
					return result;
				}
				// Continue to next attempt
				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
				await Task.Delay(delay, cancellationToken);
			} catch (Exception ex) when (attempt < maxAttempts - 1 && this.IsTransient(ex)) {
				lastException = ex;
				if (this.Logger.IsEnabled(LogLevel.Warning)) {
					this.Logger.LogWarning(ex,
					"Attempt {Attempt} of {MaxAttempts} failed, retrying...",
					attempt + 1,
					maxAttempts);
				}

				var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
				await Task.Delay(delay, cancellationToken);
			}
		}
		if (this.Logger.IsEnabled(LogLevel.Error)) {
			this.Logger.LogError(lastException,
			"All {MaxAttempts} retry attempts failed",
			maxAttempts);
		}
		throw lastException!;
	}

	/// <summary>
	/// Determines if an exception is transient and should be retried.
	/// Can be overridden in derived classes to customize retry behavior.
	/// </summary>
	/// <param name="ex">The exception to evaluate.</param>
	/// <returns>True if the exception is transient and should be retried; otherwise, false.</returns>
	protected virtual bool IsTransient(Exception ex) =>
		ex switch {
			HttpRequestException => true,
			TimeoutException => true,
			TaskCanceledException => false,
			OperationCanceledException => false,
			_ => false
		};

	#endregion

	#region JSON Operations

	/// <summary>
	/// Sends a GET request and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> GetAsync<T>(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Get.Method,
			endpoint,
			this.Client.GetAsync(endpoint, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a GET request and deserializes the JSON response to type <typeparamref name="T"/>
	/// and wraps the result in a <see cref="ResponseWithHeaders{T}"/> to include the response
	/// and content headers.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response with headers.</returns>
	protected Task<Result<ResponseWithHeaders<T>>> GetWithHeadersAsync<T>(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return this.ProcessWithTelemetryAsync(
			HttpMethod.Get.Method,
			endpoint,
			async (response, ct) => {
				var data = await response.Content.ReadFromJsonAsync<T>(this.JsonOptions, ct);
				return data is null
					? null
					: new ResponseWithHeaders<T>(data, response.Headers, response.Content.Headers);
			},
			this.Client.GetAsync(endpoint, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a POST request with JSON content and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> PostAsync<T>(
		string endpoint,
		object? content = null,
		CancellationToken cancellationToken = default) {

		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}

		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a POST request with <see cref="HttpContent"/> and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> PostAsync<T>(
		string endpoint,
		HttpContent? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};

		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a POST request with JSON content and deserializes the JSON response to type <typeparamref name="T"/>
	/// and wraps the result in a <see cref="ResponseWithHeaders{T}"/> to include the response
	/// and content headers.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response with headers.</returns>
	protected Task<Result<ResponseWithHeaders<T>>> PostWithHeadersAsync<T>(
		string endpoint,
		object? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}
		return this.ProcessWithTelemetryAsync(
			HttpMethod.Post.Method,
			endpoint,
			async (response, ct) => {
				var data = await response.Content.ReadFromJsonAsync<T>(this.JsonOptions, ct);
				return data is null
					? null
					: new ResponseWithHeaders<T>(data, response.Headers, response.Content.Headers);
			},
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a POST request with <see cref="HttpContent"/> and deserializes the JSON response to type <typeparamref name="T"/>
	/// and wraps the result in a <see cref="ResponseWithHeaders{T}"/> to include the response
	/// and content headers.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response with headers.</returns>
	protected Task<Result<ResponseWithHeaders<T>>> PostWithHeadersAsync<T>(
		string endpoint,
		HttpContent? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};
		return this.ProcessWithTelemetryAsync(
			HttpMethod.Post.Method,
			endpoint,
			async (response, ct) => {
				var data = await response.Content.ReadFromJsonAsync<T>(this.JsonOptions, ct);
				return data is null
					? null
					: new ResponseWithHeaders<T>(data, response.Headers, response.Content.Headers);
			},
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a PUT request with no body and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> PutAsync<T>(
		string endpoint,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Put.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a PUT request with JSON content and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> PutAsync<T>(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Put, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};

		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Put.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a PATCH request with no body and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> PatchAsync<T>(
		string endpoint,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint);
		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Patch.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a PATCH request with JSON content and deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> PatchAsync<T>(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {

		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};

		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Patch.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a DELETE request and optionally deserializes the JSON response to type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize the response to.</typeparam>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The deserialized response object, or default if the request failed.</returns>
	protected Task<Result<T>> DeleteAsync<T>(
		string endpoint,
		CancellationToken cancellationToken = default) {

		return this.ProcessJsonAsResultAsync<T>(
			HttpMethod.Delete.Method,
			endpoint,
			this.Client.DeleteAsync(endpoint, cancellationToken),
			cancellationToken);

	}

	#endregion

	#region String Operations

	/// <summary>
	/// Sends a GET request and returns the response content as a string.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a string, or null if the request failed.</returns>
	protected Task<Result<string>> GetAndReadStringAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {

		return this.ProcessStringAsResultAsync(
			HttpMethod.Get.Method,
			endpoint,
			this.Client.GetAsync(endpoint, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a POST request with no content and returns the response content as a string.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a string, or null if the request failed.</returns>
	protected Task<Result<string>> PostAndReadStringAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

		return this.ProcessStringAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a POST request with <see cref="HttpContent"/> and returns the response content as a string.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a string, or null if the request failed.</returns>
	protected Task<Result<string>> PostAndReadStringAsync(
		string endpoint,
		HttpContent content,
		CancellationToken cancellationToken = default) {

		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};

		return this.ProcessStringAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a POST request with JSON content and returns the response content as a string.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a string, or null if the request failed.</returns>
	protected Task<Result<string>> PostAndReadStringAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {

		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};

		return this.ProcessStringAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a PATCH request with JSON content and returns the response content as a string.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a string, or null if the request failed.</returns>
	protected Task<Result<string>> PatchAndReadStringAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {

		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};

		return this.ProcessStringAsResultAsync(
			HttpMethod.Patch.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	/// <summary>
	/// Sends a PATCH request with <see cref="HttpContent"/> and returns the response content as a string.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a string, or null if the request failed.</returns>
	protected Task<Result<string>> PatchAndReadStringAsync(
		string endpoint,
		HttpContent content,
		CancellationToken cancellationToken = default) {

		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = content
		};

		return this.ProcessStringAsResultAsync(
			HttpMethod.Patch.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);

	}

	#endregion

	#region Stream Operations

	/// <summary>
	/// Sends a GET request and returns the response content as a stream.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a stream, or null if the request failed.</returns>
	protected Task<Result<Stream>> GetAndReadStreamAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return this.ProcessStreamAsResultAsync(
			HttpMethod.Get.Method,
			endpoint,
			this.Client.GetAsync(endpoint, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a POST request with JSON content and returns the response content as a stream.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a stream, or null if the request failed.</returns>
	protected Task<Result<Stream>> PostAndReadStreamAsync(
		string endpoint,
		object? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}
		return this.ProcessStreamAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a POST request with <see cref="HttpContent"/> and returns the response content as a stream.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a stream, or null if the request failed.</returns>
	protected Task<Result<Stream>> PostAndReadStreamAsync(
		string endpoint,
		HttpContent? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};
		return this.ProcessStreamAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a PATCH request with JSON content and returns the response content as a stream.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a stream, or null if the request failed.</returns>
	protected Task<Result<Stream>> PatchAndReadStreamAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};
		return this.ProcessStreamAsResultAsync(
			HttpMethod.Patch.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a PATCH request with <see cref="HttpContent"/> and returns the response content as a stream.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a stream, or null if the request failed.</returns>
	protected Task<Result<Stream>> PatchAndReadStreamAsync(
		string endpoint,
		HttpContent content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = content
		};
		return this.ProcessStreamAsResultAsync(
			HttpMethod.Patch.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	#endregion

	#region Byte Array Operations

	/// <summary>
	/// Sends a GET request and returns the response content as a byte array.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a byte array, or null if the request failed.</returns>
	protected Task<Result<byte[]>> GetAndReadByteArrayAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return this.ProcessBytesAsResultAsync(
			HttpMethod.Get.Method,
			endpoint,
			this.Client.GetAsync(endpoint, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a POST request with JSON content and returns the response content as a byte array.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a byte array, or null if the request failed.</returns>
	protected Task<Result<byte[]>> PostAndReadByteArrayAsync(
		string endpoint,
		object? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}
		return this.ProcessBytesAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Sends a POST request with <see cref="HttpContent"/> and returns the response content as a byte array.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The response content as a byte array, or null if the request failed.</returns>
	protected Task<Result<byte[]>> PostAndReadByteArrayAsync(
		string endpoint,
		HttpContent? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};
		return this.ProcessBytesAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	#endregion

	#region File Operations

	/// <summary>
	/// Downloads a file via GET and returns the content as byte array with filename.
	/// Works in all environments including browsers.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A tuple containing the file content as a byte array and the filename, or (null, null) if the request failed.</returns>
	protected Task<Result<(byte[] Content, string FileName)>> DownloadFileContentAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return this.ProcessFileAsResultAsync(
			HttpMethod.Get.Method,
			endpoint,
			this.Client.GetAsync(endpoint, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Downloads a file via POST with JSON content and returns the content as byte array with filename.
	/// Works in all environments including browsers.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A tuple containing the file content as a byte array and the filename, or (null, null) if the request failed.</returns>
	protected Task<Result<(byte[] Content, string FileName)>> DownloadFileContentAsync(
		string endpoint,
		object? content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}
		return this.ProcessFileAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	/// <summary>
	/// Downloads a file via POST with HttpContent and returns the content as byte array with filename.
	/// Works in all environments including browsers.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A tuple containing the file content as a byte array and the filename, or (null, null) if the request failed.</returns>
	protected Task<Result<(byte[] Content, string FileName)>> DownloadFileContentAsync(
		string endpoint,
		HttpContent content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};
		return this.ProcessFileAsResultAsync(
			HttpMethod.Post.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken),
			cancellationToken);
	}

	#endregion

	#region No Response Operations

	/// <summary>
	/// Sends a GET request with no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> GetAsync(string endpoint, CancellationToken cancellationToken = default) =>
		this.ProcessWithTelemetryAsync(HttpMethod.Get.Method, endpoint, this.Client.GetAsync(endpoint, cancellationToken));

	/// <summary>
	/// Sends a POST request with JSON content and no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> PostAsync(
		string endpoint,
		object? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}
		return this.ProcessWithTelemetryAsync(HttpMethod.Post.Method, endpoint, this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a PUT request with no body and no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> PutAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Put, endpoint);
		return this.ProcessWithTelemetryAsync(HttpMethod.Put.Method, endpoint, this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a PUT request with JSON content and no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> PutAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Put, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};
		return this.ProcessWithTelemetryAsync(HttpMethod.Put.Method, endpoint, this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a PATCH request with no body and no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> PatchAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint);
		return this.ProcessWithTelemetryAsync(HttpMethod.Patch.Method, endpoint, this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a PATCH request with JSON content and no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> PatchAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};
		return this.ProcessWithTelemetryAsync(HttpMethod.Patch.Method, endpoint, this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a DELETE request with no response content processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A task that represents the asynchronous operation.</returns>
	protected Task<Result> DeleteAsync(
		string endpoint,
		CancellationToken cancellationToken = default) =>
		this.ProcessWithTelemetryAsync(HttpMethod.Delete.Method, endpoint, this.Client.DeleteAsync(endpoint, cancellationToken));

	#endregion

	#region Raw Response Operations

	/// <summary>
	/// Sends a GET request and returns the raw HttpResponseMessage for custom processing.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The HttpResponseMessage after standard error handling (401/403 throw, non-success throws ApiException).</returns>
	/// <remarks>
	/// Use this when you need direct access to response headers, status codes, or want to 
	/// process the response content yourself. Consumer is responsible for disposing the response.
	/// </remarks>
	protected async Task<HttpResponseMessage> GetRawAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return await this.ProcessResponseAsync(this.Client.GetAsync(endpoint, cancellationToken));
	}

	/// <summary>
	/// Sends a POST request with JSON content and returns the raw HttpResponseMessage.
	/// Consumer is responsible for disposing the response.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content, or null for no body.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The raw HttpResponseMessage, or null if the request failed.</returns>
	protected async Task<HttpResponseMessage> PostRawAsync(
		string endpoint,
		object? content = null,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
		if (content is not null) {
			request.Content = JsonContent.Create(content, options: this.JsonOptions);
		}
		return await this.ProcessResponseAsync(this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a POST request with HttpContent and returns the raw HttpResponseMessage.
	/// Consumer is responsible for disposing the response.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The HTTP content to send.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The raw HttpResponseMessage, or null if the request failed.</returns>
	protected async Task<HttpResponseMessage> PostRawAsync(
		string endpoint,
		HttpContent content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Post, endpoint) {
			Content = content
		};
		return await this.ProcessResponseAsync(this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a PUT request with JSON content and returns the raw HttpResponseMessage.
	/// Consumer is responsible for disposing the response.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The raw HttpResponseMessage, or null if the request failed.</returns>
	protected async Task<HttpResponseMessage> PutRawAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Put, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};
		return await this.ProcessResponseAsync(this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a PATCH request with JSON content and returns the raw HttpResponseMessage.
	/// Consumer is responsible for disposing the response.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="content">The object to serialize as JSON content.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The raw HttpResponseMessage, or null if the request failed.</returns>
	protected async Task<HttpResponseMessage> PatchRawAsync(
		string endpoint,
		object content,
		CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) {
			Content = JsonContent.Create(content, options: this.JsonOptions)
		};
		return await this.ProcessResponseAsync(this.Client.SendAsync(request, cancellationToken));
	}

	/// <summary>
	/// Sends a DELETE request and returns the raw HttpResponseMessage.
	/// Consumer is responsible for disposing the response.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>The raw HttpResponseMessage, or null if the request failed.</returns>
	protected async Task<HttpResponseMessage> DeleteRawAsync(
		string endpoint,
		CancellationToken cancellationToken = default) {
		return await this.ProcessResponseAsync(this.Client.DeleteAsync(endpoint, cancellationToken));
	}

	#endregion

	#region HEAD Operations

	/// <summary>
	/// Sends a HEAD request to check resource existence without downloading the body.
	/// </summary>
	/// <param name="endpoint">The API endpoint to call.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>A Result indicating success/failure.</returns>
	protected Task<Result> ResourceExistsAsync(string endpoint, CancellationToken cancellationToken = default) {
		var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
		return this.ProcessWithTelemetryAsync(
			HttpMethod.Head.Method,
			endpoint,
			this.Client.SendAsync(request, cancellationToken));
	}

	#endregion

	#region Processors

	/// <summary>
	/// Core processor that wraps any operation with telemetry and exception handling.
	/// </summary>
	private async Task<Result<T>> ProcessWithTelemetryAsync<T>(
		string method,
		string endpoint,
		Func<HttpResponseMessage, CancellationToken, Task<T?>> contentProcessor,
		Task<HttpResponseMessage> action,
		CancellationToken cancellationToken) {
		using var activity = RemoteClientTelemetry.StartActivity(method, endpoint, this.TypeName);
		var startTimestamp = Timing.Start();

		try {

			var response = await this.ProcessResponseAsync(action);

			// ERROR (technically should never be null)
			if (response is null) {
				var nullElapsed = Timing.GetElapsedMilliseconds(startTimestamp);
				var error = new InvalidOperationException("Response was null");
				RemoteClientTelemetry.SetActivityError(activity, error);
				RemoteClientTelemetry.RecordFailure(method, endpoint, this.TypeName, null, nullElapsed, error, this.Logger);
				return Result<T>.Fail(error);
			}

			var data = await contentProcessor(response, cancellationToken);
			var elapsed = Timing.GetElapsedMilliseconds(startTimestamp);

			// ERROR
			if (data is null) {
				var error = new InvalidOperationException("Content processing returned null");
				RemoteClientTelemetry.SetActivityError(activity, error, (int)response.StatusCode);
				RemoteClientTelemetry.RecordFailure(method, endpoint, this.TypeName, (int)response.StatusCode, elapsed, error, this.Logger);
				return Result<T>.Fail(error);
			}

			// SUCCESS
			RemoteClientTelemetry.SetActivitySuccess(activity, (int)response.StatusCode);
			RemoteClientTelemetry.RecordSuccess(method, endpoint, this.TypeName, (int)response.StatusCode, elapsed, this.Logger);
			return Result<T>.Success(data);

		} catch (Exception fex) when (fex.IsFatal()) {
			throw;
		} catch (Exception ex) {
			var edi = ExceptionDispatchInfo.Capture(ex);
			var error = this.HandleRemoteExceptionCore(
				edi,
				method,
				endpoint,
				this.TypeName,
				startTimestamp,
				activity);

			return Result<T>.Fail(error);
		}
	}

	/// <summary>
	/// Core processor for operations with no response body, wraps with telemetry and exception handling.
	/// Returns Result (not Result&lt;T&gt;) for operations that don't need response content.
	/// </summary>
	private async Task<Result> ProcessWithTelemetryAsync(
		string method,
		string endpoint,
		Task<HttpResponseMessage> action) {
		using var activity = RemoteClientTelemetry.StartActivity(method, endpoint, this.TypeName);
		var startTimestamp = Timing.Start();

		try {
			var response = await this.ProcessResponseAsync(action);
			var elapsed = Timing.GetElapsedMilliseconds(startTimestamp);

			// ERROR (technically should never be null)
			if (response is null) {
				var error = new InvalidOperationException("Response was null");
				RemoteClientTelemetry.SetActivityError(activity, error);
				RemoteClientTelemetry.RecordFailure(method, endpoint, this.TypeName, null, elapsed, error, this.Logger);
				return Result.Fail(error);
			}

			// SUCCESS - no content to process
			RemoteClientTelemetry.SetActivitySuccess(activity, (int)response.StatusCode);
			RemoteClientTelemetry.RecordSuccess(method, endpoint, this.TypeName, (int)response.StatusCode, elapsed, this.Logger);
			return Result.Success;

		} catch (Exception fex) when (fex.IsFatal()) {
			throw;
		} catch (Exception ex) {
			var edi = ExceptionDispatchInfo.Capture(ex);
			var error = this.HandleRemoteExceptionCore(
				edi,
				method,
				endpoint,
				this.TypeName,
				startTimestamp,
				activity);

			return Result.Fail(error);
		}
	}

	private Exception HandleRemoteExceptionCore(
		ExceptionDispatchInfo edi,
		string method,
		string endpoint,
		string typeName,
		long startTimestamp,
		Activity? activity) {

		var elapsed = Timing.GetElapsedMilliseconds(startTimestamp);
		var ex = edi.SourceException;

		switch (ex) {
			case UnauthenticatedAccessException uae:
				RemoteClientTelemetry.SetActivityError(activity, uae, 401);
				RemoteClientTelemetry.RecordFailure(method, endpoint, typeName, 401, elapsed, uae, this.Logger);
				edi.Throw(); // never returns
				throw new UnreachableException(); // compiler satisfaction + intent

			case ForbiddenAccessException fae:
				RemoteClientTelemetry.SetActivityError(activity, fae, 403);
				RemoteClientTelemetry.RecordFailure(method, endpoint, typeName, 403, elapsed, fae, this.Logger);
				edi.Throw(); // never returns
				throw new UnreachableException(); // compiler satisfaction + intent

			case OperationCanceledException oce:
				RemoteClientTelemetry.SetActivityCanceled(activity, oce);
				RemoteClientTelemetry.RecordCanceled(method, endpoint, typeName, elapsed, oce, this.Logger);
				edi.Throw(); // never returns
				throw new UnreachableException(); // compiler satisfaction + intent

			case HttpRequestException hre:
				RemoteClientTelemetry.SetActivityError(activity, hre, (int?)hre.StatusCode);
				RemoteClientTelemetry.RecordFailure(method, endpoint, typeName, (int?)hre.StatusCode, elapsed, hre, this.Logger);
				return hre;

			default:
				RemoteClientTelemetry.SetActivityError(activity, ex);
				RemoteClientTelemetry.RecordFailure(method, endpoint, typeName, null, elapsed, ex, this.Logger);
				return ex;
		}

	}


	/// <summary>
	/// Processes HTTP response into Result&lt;T&gt;.
	/// Handles success and API exceptions consistently
	/// and throws for authN/authZ.
	/// </summary>
	private Task<Result<T>> ProcessJsonAsResultAsync<T>(
		string method,
		string endpoint,
		Task<HttpResponseMessage> action,
		CancellationToken cancellationToken) {
		return this.ProcessWithTelemetryAsync(
			method,
			endpoint,
			async (response, ct) => await response.Content.ReadFromJsonAsync<T>(this.JsonOptions, ct),
			action,
			cancellationToken);
	}

	/// <summary>
	/// Processes HTTP response into Result&lt;string&gt;.
	/// Handles success and API exceptions consistently
	/// and throws for authN/authZ.
	/// </summary>
	private Task<Result<string>> ProcessStringAsResultAsync(
		string method,
		string endpoint,
		Task<HttpResponseMessage> action,
		CancellationToken cancellationToken) {
		return this.ProcessWithTelemetryAsync(
			method,
			endpoint,
			async (response, ct) => await response.Content.ReadAsStringAsync(ct),
			action,
			cancellationToken);
	}

	/// <summary>
	/// Processes HTTP response into Result&lt;string&gt;.
	/// Handles success and API exceptions consistently
	/// and throws for authN/authZ.
	/// </summary>
	private Task<Result<Stream>> ProcessStreamAsResultAsync(
		string method,
		string endpoint,
		Task<HttpResponseMessage> action,
		CancellationToken cancellationToken) {
		return this.ProcessWithTelemetryAsync(
			method,
			endpoint,
			async (response, ct) => await response.Content.ReadAsStreamAsync(ct),
			action,
			cancellationToken);
	}

	/// <summary>
	/// Processes HTTP response into Result&lt;string&gt;.
	/// Handles success and API exceptions consistently
	/// and throws for authN/authZ.
	/// </summary>
	private Task<Result<byte[]>> ProcessBytesAsResultAsync(
	string method,
	string endpoint,
	Task<HttpResponseMessage> action,
	CancellationToken cancellationToken) {
		return this.ProcessWithTelemetryAsync(
			method,
			endpoint,
			async (response, ct) => await response.Content.ReadAsByteArrayAsync(ct),
			action,
			cancellationToken);
	}

	/// <summary>
	/// Processes HTTP response into Result&lt;string&gt;.
	/// Handles success and API exceptions consistently
	/// and throws for authN/authZ.
	/// </summary>
	private Task<Result<(byte[] Content, string FileName)>> ProcessFileAsResultAsync(
		string method,
		string endpoint,
		Task<HttpResponseMessage> action,
		CancellationToken cancellationToken) {
		return this.ProcessWithTelemetryAsync<(byte[] Content, string FileName)>(
			method,
			endpoint,
			async (response, ct) => {
				var data = await response.Content.ReadAsByteArrayAsync(ct);
				var fileName =
					response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ??
					response.Content.Headers.ContentDisposition?.Name?.Trim('"');

				if (!string.IsNullOrEmpty(fileName)) {
					fileName = Uri.UnescapeDataString(fileName);
				}
				fileName ??= "unknown";

				return (data, fileName);
			},
			action,
			cancellationToken);
	}

	/// <summary>
	/// Processes the HTTP response and handles common error scenarios.
	/// </summary>
	/// <param name="action">The HTTP request task to process.</param>
	/// <returns>The HTTP response message if successful, or null if an error occurred.</returns>
	private async Task<HttpResponseMessage> ProcessResponseAsync(Task<HttpResponseMessage> action) {
		var response = await action;
		if (response.IsSuccessStatusCode) {
			var contentType = response.Content.Headers.ContentType?.MediaType;
			if (contentType == "application/problem+json") {
				// Server sent error as 200 - treat as error
				var resultExceptionModel = await this.ParseErrorResponse(response);
				throw new ApiException(resultExceptionModel);
			}
			return response;
		}

		if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) {
			var err = await response.Content.ReadAsStringAsync();
			throw new UnauthenticatedAccessException(err);
		}

		if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) {
			var err = await response.Content.ReadAsStringAsync();
			throw new ForbiddenAccessException(err);
		}

		var exceptionModel = await this.ParseErrorResponse(response);
		if (this.Logger.IsEnabled(LogLevel.Error)) {
			this.Logger.LogError("API error: {Title}. Details: {Detail}",
				exceptionModel.Title,
				exceptionModel.Detail);
		}
		throw new ApiException(exceptionModel);
	}

	/// <summary>
	/// Parses error response content into an ExceptionModel.
	/// </summary>
	/// <param name="response">The HTTP response containing the error.</param>
	/// <returns>An ExceptionModel representing the error details.</returns>
	private async Task<ExceptionModel> ParseErrorResponse(HttpResponseMessage response) {
		try {
			return await response.Content.ReadFromJsonAsync<ExceptionModel>(this.JsonOptions)
				?? CreateDefaultExceptionModel(response.ReasonPhrase);
		} catch {
			string? message = null;
			try {
				message = await response.Content.ReadAsStringAsync();
			} catch (Exception ex) {
				this.Logger.LogWarning(ex, "Failed to read error response content");
			}

			return CreateDefaultExceptionModel(message ?? response.ReasonPhrase);
		}
	}

	/// <summary>
	/// Creates a default exception model when error parsing fails.
	/// </summary>
	/// <param name="detail">The error detail message.</param>
	/// <returns>A default ExceptionModel with the provided detail.</returns>
	private static ExceptionModel CreateDefaultExceptionModel(string? detail) => new() {
		Detail = detail ?? "Unknown error",
		Title = "Unknown Error",
		Failures = []
	};


	#endregion

}