using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace FileStorage
{
    [Route("{*path}")]
    [ApiController]
    public class StorageController : ControllerBase
    {
        private readonly string _storageRoot;
        private readonly ILogger<StorageController> _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public StorageController(string storageRoot, ILogger<StorageController> logger)
        {
            _storageRoot = storageRoot;
            _logger = logger;
            _contentTypeProvider = new FileExtensionContentTypeProvider();
        }

        private string GetSafePath(string virtualPath)
        {
            virtualPath = virtualPath?.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar) ?? string.Empty;

            var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, virtualPath));

            if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Доступ запрещён: попытка выйти за пределы корня хранилища.");

            return fullPath;
        }

        [HttpGet]
        public async Task<IActionResult> Get(string path)
        {
            try
            {
                var physicalPath = GetSafePath(path);

                if (System.IO.File.Exists(physicalPath))
                {
                    var fileStream = System.IO.File.OpenRead(physicalPath);
                    var contentType = _contentTypeProvider.TryGetContentType(physicalPath, out var mime)
                        ? mime : "application/octet-stream";
                    return File(fileStream, contentType);
                }
                else if (Directory.Exists(physicalPath))
                {
                    var directoryInfo = new DirectoryInfo(physicalPath);
                    var entries = new List<object>();

                    foreach (var file in directoryInfo.GetFiles())
                    {
                        entries.Add(new
                        {
                            name = file.Name,
                            type = "file",
                            size = file.Length,
                            lastModified = file.LastWriteTimeUtc.ToString("o")
                        });
                    }

                    foreach (var dir in directoryInfo.GetDirectories())
                    {
                        entries.Add(new
                        {
                            name = dir.Name,
                            type = "directory",
                            size = (long?)null,
                            lastModified = dir.LastWriteTimeUtc.ToString("o")
                        });
                    }

                    return Ok(entries);
                }
                else
                {
                    return NotFound($"Путь '{path}' не существует.");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке GET-запроса для пути {Path}", path);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpPut]
        public async Task<IActionResult> Put(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return BadRequest("Путь к файлу не может быть пустым.");

                var physicalPath = GetSafePath(path);

                if (Directory.Exists(physicalPath))
                    return BadRequest("Нельзя перезаписать каталог файлом.");

                var directory = Path.GetDirectoryName(physicalPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (var fileStream = System.IO.File.Create(physicalPath))
                {
                    await Request.Body.CopyToAsync(fileStream);
                }

                bool wasCreated = System.IO.File.Exists(physicalPath); 

                return Ok($"Файл '{path}' успешно сохранён.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке PUT-запроса для пути {Path}", path);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpHead]
        public IActionResult Head(string path)
        {
            try
            {
                var physicalPath = GetSafePath(path);

                if (System.IO.File.Exists(physicalPath))
                {
                    var fileInfo = new FileInfo(physicalPath);
                    Response.Headers.Append("Content-Length", fileInfo.Length.ToString());
                    Response.Headers.Append("Last-Modified", fileInfo.LastWriteTimeUtc.ToString("R"));
                   
                    var contentType = _contentTypeProvider.TryGetContentType(physicalPath, out var mime)
                        ? mime : "application/octet-stream";
                    Response.Headers.Append("Content-Type", contentType);
                    return Ok();
                }
                else
                {
                    return NotFound($"Файл '{path}' не существует.");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке HEAD-запроса для пути {Path}", path);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpDelete]
        public IActionResult Delete(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return BadRequest("Путь не может быть пустым.");

                var physicalPath = GetSafePath(path);

                if (physicalPath.Equals(_storageRoot, StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Нельзя удалить корневой каталог хранилища.");

                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);
                    return Ok($"Файл '{path}' успешно удалён.");
                }
                else if (Directory.Exists(physicalPath))
                {
                    Directory.Delete(physicalPath, recursive: true);
                    return Ok($"Каталог '{path}' успешно удалён.");
                }
                else
                {
                    return NotFound($"Путь '{path}' не существует.");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке DELETE-запроса для пути {Path}", path);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }
    }
}