param(
    [string]$BaseUrl = 'http://localhost:5080',
    [switch]$IncludeBoundaryStress
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$apiRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $apiRoot '..\..')).Path
$settingsPath = Join-Path $apiRoot 'appsettings.Development.json'
if (-not (Test-Path $settingsPath)) {
    throw "Missing $settingsPath. Copy appsettings.Development.json.example and configure it first."
}

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.UseCookies = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [uri]$BaseUrl.TrimEnd('/')
$script:failures = 0
$threadIds = [System.Collections.Generic.List[int]]::new()
$userAId = $null
$userBId = $null
$temporaryAdminId = $null
$temporaryBoardId = $null
$rootToken = $null
$uploadedAttachmentPath = $null
$replyDeleteImagePath = $null

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token = $null,
        [object]$Body = $null,
        [System.Net.Http.HttpContent]$Content = $null,
        [string]$Cookie = $null
    )

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method), $Path)
    if ($Token) {
        $request.Headers.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $Token)
    }
    if ($Cookie) {
        $request.Headers.Add('Cookie', $Cookie)
    }
    if ($Content) {
        $request.Content = $Content
    }
    elseif ($null -ne $Body) {
        $json = ConvertTo-Json $Body -Depth 10 -Compress
        $request.Content = [System.Net.Http.StringContent]::new(
            $json, [System.Text.Encoding]::UTF8, 'application/json')
    }

    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{
            Status = [int]$response.StatusCode
            Body = $responseBody
            Headers = $response.Headers.ToString() + $response.Content.Headers.ToString()
        }
    }
    finally {
        $request.Dispose()
    }
}

function New-Form {
    param([hashtable]$Fields)
    $form = [System.Net.Http.MultipartFormDataContent]::new()
    foreach ($name in $Fields.Keys) {
        $form.Add([System.Net.Http.StringContent]::new([string]$Fields[$name]), $name)
    }
    return ,$form
}

function Add-Image {
    param(
        [System.Net.Http.MultipartFormDataContent]$Form,
        [byte[]]$Bytes,
        [string]$FileName,
        [string]$ContentType
    )
    $file = [System.Net.Http.ByteArrayContent]::new($Bytes)
    $file.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($ContentType)
    $Form.Add($file, 'Image', $FileName)
}

function Invoke-ConcurrentRefresh {
    param([string]$RawRefreshToken)

    $requests = [System.Collections.Generic.List[System.Net.Http.HttpRequestMessage]]::new()
    $tasks = [System.Collections.Generic.List[System.Threading.Tasks.Task[System.Net.Http.HttpResponseMessage]]]::new()
    try {
        1..2 | ForEach-Object {
            $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, '/api/auth/refresh')
            $request.Headers.Add('Cookie', "refreshToken=$RawRefreshToken")
            $requests.Add($request)
            $tasks.Add($client.SendAsync($request))
        }
        [System.Threading.Tasks.Task]::WaitAll($tasks.ToArray())
        return @($tasks | ForEach-Object { [int]$_.Result.StatusCode })
    }
    finally {
        foreach ($request in $requests) { $request.Dispose() }
        foreach ($task in $tasks) { if ($task.Status -eq [System.Threading.Tasks.TaskStatus]::RanToCompletion) { $task.Result.Dispose() } }
    }
}

function Assert-Stage8 {
    param([string]$Name, [bool]$Passed, [string]$Detail = '')
    if ($Passed) {
        Write-Output "PASS $Name $Detail"
    }
    else {
        $script:failures++
        Write-Output "FAIL $Name $Detail"
    }
}

function Read-Json($Response) {
    if (-not $Response.Body) { return $null }
    return ConvertFrom-Json $Response.Body
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$usernameA = "e2e_a_$stamp"
$usernameB = "e2e_b_$stamp"
$emailA = "$usernameA@funforum.local"
$emailB = "$usernameB@funforum.local"
$testPassword = 'E2E!Funforum_Stage8_2026'

try {
    $health = Invoke-Api 'GET' '/api/health'
    if ($health.Status -ne 200) {
        throw "API is not ready at $BaseUrl (health status $($health.Status))."
    }

    $registerA = Invoke-Api 'POST' '/api/auth/register' -Body @{
        username = $usernameA; email = $emailA; password = $testPassword
    }
    $registerB = Invoke-Api 'POST' '/api/auth/register' -Body @{
        username = $usernameB; email = $emailB; password = $testPassword
    }
    $userAId = (Read-Json $registerA).id
    $userBId = (Read-Json $registerB).id
    Assert-Stage8 'register two users' ($registerA.Status -eq 201 -and $registerB.Status -eq 201) "$($registerA.Status)/$($registerB.Status)"

    $duplicateName = Invoke-Api 'POST' '/api/auth/register' -Body @{
        username = $usernameA; email = "other_$stamp@funforum.local"; password = $testPassword
    }
    $duplicateEmail = Invoke-Api 'POST' '/api/auth/register' -Body @{
        username = "e2e_c_$stamp"; email = $emailA; password = $testPassword
    }
    Assert-Stage8 'duplicate username and email rejected' ($duplicateName.Status -eq 409 -and $duplicateEmail.Status -eq 409) "$($duplicateName.Status)/$($duplicateEmail.Status)"
    $reservedName = Invoke-Api 'POST' '/api/auth/register' -Body @{
        username = 'admin'; email = "reserved_$stamp@funforum.local"; password = $testPassword
    }
    Assert-Stage8 'reserved username rejected' ($reservedName.Status -eq 400) "$($reservedName.Status)"

    $loginA = Invoke-Api 'POST' '/api/auth/login' -Body @{ username = $usernameA; password = $testPassword }
    $loginB = Invoke-Api 'POST' '/api/auth/login' -Body @{ username = $usernameB; password = $testPassword }
    $tokenA = (Read-Json $loginA).accessToken
    $tokenB = (Read-Json $loginB).accessToken
    Assert-Stage8 'login both users' ($loginA.Status -eq 200 -and $loginB.Status -eq 200 -and $tokenA -and $tokenB) "$($loginA.Status)/$($loginB.Status)"

    $oldRefresh = [regex]::Match($loginA.Headers, '(?im)Set-Cookie:\s*refreshToken=([^;]+)').Groups[1].Value
    $refresh = Invoke-Api 'POST' '/api/auth/refresh' -Cookie "refreshToken=$oldRefresh"
    $newRefresh = [regex]::Match($refresh.Headers, '(?im)Set-Cookie:\s*refreshToken=([^;]+)').Groups[1].Value
    $reusedRefresh = Invoke-Api 'POST' '/api/auth/refresh' -Cookie "refreshToken=$oldRefresh"
    Assert-Stage8 'refresh token rotates and rejects reuse' ($refresh.Status -eq 200 -and $newRefresh -and $oldRefresh -ne $newRefresh -and $reusedRefresh.Status -eq 401) "$($refresh.Status)/$($reusedRefresh.Status)"
    $refreshB = [regex]::Match($loginB.Headers, '(?im)Set-Cookie:\s*refreshToken=([^;]+)').Groups[1].Value
    $concurrentRefreshStatuses = Invoke-ConcurrentRefresh $refreshB
    $concurrentRefreshPassed = @($concurrentRefreshStatuses | Where-Object { $_ -eq 200 }).Count -eq 1 -and @($concurrentRefreshStatuses | Where-Object { $_ -eq 401 }).Count -eq 1
    Assert-Stage8 'concurrent refresh allows exactly one request' $concurrentRefreshPassed ($concurrentRefreshStatuses -join '/')

    $board = Invoke-Api 'GET' '/api/boards/Tech'
    Assert-Stage8 'board slug lookup ignores case' ($board.Status -eq 200 -and (Read-Json $board).slug -eq 'tech') "$($board.Status)"

    $capcodeForm = New-Form @{ Title = 'E2E denied capcode'; Content = 'Should be rejected'; UseCapcode = 'true' }
    $capcodeResult = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $capcodeForm
    Assert-Stage8 'user cannot request capcode' ($capcodeResult.Status -eq 403) "$($capcodeResult.Status)"

    $blankForm = New-Form @{ Title = '   '; Content = 'Nonblank content'; UseCapcode = 'false' }
    $blankTitle = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $blankForm
    $longForm = New-Form @{ Title = ('x' * 201); Content = 'Over limit'; UseCapcode = 'false' }
    $longTitle = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $longForm
    $blankContent = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content (New-Form @{ Title = 'Valid title'; Content = '   '; UseCapcode = 'false' })
    $longContent = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content (New-Form @{ Title = 'Valid title'; Content = ('x' * 10001); UseCapcode = 'false' })
    Assert-Stage8 'blank and overlong thread title/content rejected' ($blankTitle.Status -eq 400 -and $longTitle.Status -eq 400 -and $blankContent.Status -eq 400 -and $longContent.Status -eq 400) "$($blankTitle.Status)/$($longTitle.Status)/$($blankContent.Status)/$($longContent.Status)"

    $title = "E2E marker%_slash\$stamp"
    $threadForm = New-Form @{ Title = $title; Content = 'Stage 8 user journey'; UseCapcode = 'false' }
    $threadResponse = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $threadForm
    $thread = Read-Json $threadResponse
    if ($threadResponse.Status -eq 201) { $threadIds.Add([int]$thread.id) }
    Assert-Stage8 'user creates thread' ($threadResponse.Status -eq 201) "$($threadResponse.Status)"

    foreach ($query in '%', '_', '\') {
        $encoded = [uri]::EscapeDataString($query)
        $search = Invoke-Api 'GET' "/api/boards/tech/threads?q=$encoded&pageSize=50"
        $items = (Read-Json $search).items
        $matched = @($items | Where-Object { $_.id -eq $thread.id }).Count -eq 1
        Assert-Stage8 "literal search character '$query'" ($search.Status -eq 200 -and $matched) "$($search.Status)"
    }

    $publicThread = Invoke-Api 'GET' "/api/threads/$($thread.id)"
    Assert-Stage8 'public thread response hides internal author IDs' ($publicThread.Status -eq 200 -and $publicThread.Body -notmatch 'author_user_id|author_admin_id') "$($publicThread.Status)"
    $foreignUpdate = Invoke-Api 'PUT' "/api/threads/$($thread.id)" -Token $tokenB -Body @{ title = 'Denied'; content = 'Denied' }
    $foreignDelete = Invoke-Api 'DELETE' "/api/threads/$($thread.id)" -Token $tokenB
    Assert-Stage8 'user B cannot edit or delete user A thread' ($foreignUpdate.Status -eq 403 -and $foreignDelete.Status -eq 403) "$($foreignUpdate.Status)/$($foreignDelete.Status)"

    $replyForm = New-Form @{ Content = 'Reply from user B'; UseCapcode = 'false' }
    $replyResponse = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenB -Content $replyForm
    $reply = Read-Json $replyResponse
    Assert-Stage8 'user B replies to thread' ($replyResponse.Status -eq 201) "$($replyResponse.Status)"
    Assert-Stage8 'public reply response hides internal author IDs' ($replyResponse.Status -eq 201 -and $replyResponse.Body -notmatch 'author_user_id|author_admin_id') "$($replyResponse.Status)"

    $emptyReply = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content (New-Form @{ Content = ''; UseCapcode = 'false' })
    $longReply = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content (New-Form @{ Content = ('x' * 5001); UseCapcode = 'false' })
    Assert-Stage8 'empty and overlong replies rejected' ($emptyReply.Status -eq 400 -and $longReply.Status -eq 400) "$($emptyReply.Status)/$($longReply.Status)"

    $gifBytes = [Convert]::FromBase64String('R0lGODlhCAAIAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQADAAAACwAAAAACAAIAAAIDwABCBxIsKDBgwgTKkwYEAAh+QQADAAAACwAAAAACAAIAIEAAP8AAAAAAAAAAAAIDwABCBxIsKDBgwgTKkwYEAA7')
    $imageOnlyForm = New-Form @{ Content = ''; UseCapcode = 'false' }
    Add-Image $imageOnlyForm $gifBytes 'pixel.gif' 'text/html'
    $imageOnlyReply = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content $imageOnlyForm
    $imageOnlyDto = Read-Json $imageOnlyReply
    if ($imageOnlyReply.Status -eq 201) { $uploadedAttachmentPath = $imageOnlyDto.image.url }
    Assert-Stage8 'image-only reply accepts image with spoofed content type' ($imageOnlyReply.Status -eq 201 -and $imageOnlyDto.image.url -and $imageOnlyDto.image.thumbUrl) "$($imageOnlyReply.Status)"
    if ($imageOnlyReply.Status -eq 201) {
        $storedGifBytes = $client.GetByteArrayAsync("$($BaseUrl.TrimEnd('/'))$($imageOnlyDto.image.url)").GetAwaiter().GetResult()
        $storedGifStream = [System.IO.MemoryStream]::new($storedGifBytes)
        $storedGif = [System.Drawing.Image]::FromStream($storedGifStream)
        $storedGifFrames = $storedGif.GetFrameCount([System.Drawing.Imaging.FrameDimension]::Time)
        $thumbBytes = $client.GetByteArrayAsync("$($BaseUrl.TrimEnd('/'))$($imageOnlyDto.image.thumbUrl)").GetAwaiter().GetResult()
        $thumbStream = [System.IO.MemoryStream]::new($thumbBytes)
        $thumb = [System.Drawing.Bitmap]::new($thumbStream)
        $thumbPixel = $thumb.GetPixel(0, 0)
        Assert-Stage8 'animated GIF remains animated after upload' ($storedGifFrames -eq 2) "frames=$storedGifFrames"
        Assert-Stage8 'GIF thumbnail uses first red frame' ($thumbPixel.R -gt 200 -and $thumbPixel.R -gt ($thumbPixel.B * 2)) "rgb=$($thumbPixel.R),$($thumbPixel.G),$($thumbPixel.B)"
        $thumb.Dispose()
        $thumbStream.Dispose()
        $storedGif.Dispose()
        $storedGifStream.Dispose()
    }

    $oversizeForm = New-Form @{ Title = 'E2E oversize image'; Content = 'Reject over 4 MB'; UseCapcode = 'false' }
    Add-Image $oversizeForm ([byte[]]::new((4 * 1024 * 1024) + 1)) 'oversize.png' 'image/png'
    $oversizeImage = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $oversizeForm
    Assert-Stage8 'image larger than 4 MB rejected' ($oversizeImage.Status -eq 400) "$($oversizeImage.Status)"

    Add-Type -AssemblyName System.Drawing
    $wideBitmap = [System.Drawing.Bitmap]::new(6001, 1)
    $wideStream = [System.IO.MemoryStream]::new()
    try {
        $wideBitmap.Save($wideStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $wideImageForm = New-Form @{ Title = 'E2E wide image'; Content = 'Reject over 6000 px'; UseCapcode = 'false' }
        Add-Image $wideImageForm $wideStream.ToArray() 'wide.png' 'image/png'
        $wideImage = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $wideImageForm
        Assert-Stage8 'image wider than 6000 px rejected' ($wideImage.Status -eq 400) "$($wideImage.Status)"
    }
    finally {
        $wideBitmap.Dispose()
        $wideStream.Dispose()
    }

    $foreignReplyUpdate = Invoke-Api 'PUT' "/api/replies/$($reply.id)" -Token $tokenA -Body @{ content = 'Denied edit' }
    Assert-Stage8 'thread owner cannot edit another author reply' ($foreignReplyUpdate.Status -eq 403) "$($foreignReplyUpdate.Status)"

    $childForm = New-Form @{ Content = 'Nested reply from user A'; ParentReplyId = [string]$reply.id; UseCapcode = 'false' }
    $childResponse = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content $childForm
    $childReply = Read-Json $childResponse
    Assert-Stage8 'nested reply creation' ($childResponse.Status -eq 201) "$($childResponse.Status)"
    $previewReplyOne = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content (New-Form @{ Content = 'Preview bump one'; UseCapcode = 'false' })
    $previewReplyTwo = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content (New-Form @{ Content = 'Preview bump two'; UseCapcode = 'false' })
    $previewQuery = [uri]::EscapeDataString($title)
    $previewFeed = Invoke-Api 'GET' "/api/boards/tech/threads?q=$previewQuery&pageSize=10"
    $previewThread = (Read-Json $previewFeed).items | Where-Object { $_.id -eq $thread.id } | Select-Object -First 1
    Assert-Stage8 'board index returns 3 previews and omitted reply/image counts' ($previewReplyOne.Status -eq 201 -and $previewReplyTwo.Status -eq 201 -and $previewThread.previewReplies.Count -eq 3 -and $previewThread.omittedReplies -eq 2 -and $previewThread.omittedImages -eq 1) "$($previewFeed.Status)/previews=$($previewThread.previewReplies.Count)/omitted=$($previewThread.omittedReplies)/images=$($previewThread.omittedImages)"
    $deleteImageForm = New-Form @{ Content = 'Image attachment deletion fixture'; UseCapcode = 'false' }
    Add-Image $deleteImageForm $gifBytes 'delete-me.gif' 'image/gif'
    $deleteImageReply = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content $deleteImageForm
    $deleteImageDto = Read-Json $deleteImageReply
    if ($deleteImageReply.Status -eq 201) { $replyDeleteImagePath = $deleteImageDto.image.url }
    $deletedImageReply = if ($deleteImageReply.Status -eq 201) { Invoke-Api 'DELETE' "/api/replies/$($deleteImageDto.id)" -Token $tokenA } else { $null }
    $removedReplyImage = if ($replyDeleteImagePath) { Invoke-Api 'GET' $replyDeleteImagePath } else { $null }
    Assert-Stage8 'deleting a reply removes its attachment file' ($deleteImageReply.Status -eq 201 -and $deletedImageReply.Status -eq 204 -and $removedReplyImage.Status -eq 404) "$($deleteImageReply.Status)/$($deletedImageReply.Status)/$($removedReplyImage.Status)"
    $foreignChildUpdate = Invoke-Api 'PUT' "/api/replies/$($childReply.id)" -Token $tokenB -Body @{ content = 'Denied edit' }
    $foreignChildDelete = Invoke-Api 'DELETE' "/api/replies/$($childReply.id)" -Token $tokenB
    Assert-Stage8 'user B cannot edit or delete user A reply' ($foreignChildUpdate.Status -eq 403 -and $foreignChildDelete.Status -eq 403) "$($foreignChildUpdate.Status)/$($foreignChildDelete.Status)"

    $notificationsA = Invoke-Api 'GET' '/api/notifications' -Token $tokenA
    $notificationsB = Invoke-Api 'GET' '/api/notifications' -Token $tokenB
    $notificationItemsA = (Read-Json $notificationsA).items
    $notificationItemsB = (Read-Json $notificationsB).items
    $aHasRootReply = @($notificationItemsA | Where-Object { $_.replyId -eq $reply.id }).Count -eq 1
    $aHasNestedReply = @($notificationItemsA | Where-Object { $_.replyId -eq $childReply.id }).Count -gt 0
    $bHasNestedReply = @($notificationItemsB | Where-Object { $_.replyId -eq $childReply.id }).Count -eq 1
    Assert-Stage8 'notification goes to thread owner for root reply' ($notificationsA.Status -eq 200 -and $aHasRootReply) "$($notificationsA.Status)"
    Assert-Stage8 'nested reply notifies parent author, not thread owner' ($notificationsB.Status -eq 200 -and $bHasNestedReply -and -not $aHasNestedReply) "$($notificationsB.Status)/$($notificationsA.Status)"
    $rootNotification = $notificationItemsA | Where-Object { $_.replyId -eq $reply.id } | Select-Object -First 1
    $markRead = Invoke-Api 'POST' "/api/notifications/$($rootNotification.id)/read" -Token $tokenA
    $unreadAfterRead = (Read-Json (Invoke-Api 'GET' '/api/notifications/unread-count' -Token $tokenA)).count
    Assert-Stage8 'mark notification read updates unread count' ($markRead.Status -eq 204 -and $unreadAfterRead -eq 0) "$($markRead.Status)/$unreadAfterRead"

    $hideReply = Invoke-Api 'PATCH' "/api/replies/$($reply.id)/status" -Token $tokenA -Body @{ status = 'hidden' }
    $hiddenChild = Invoke-Api 'GET' "/api/replies/$($childReply.id)"
    $hiddenParentReply = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = 'Cannot reply to hidden parent'; ParentReplyId = [string]$reply.id; UseCapcode = 'false' })
    $hiddenUnreadA = (Read-Json (Invoke-Api 'GET' '/api/notifications/unread-count' -Token $tokenA)).count
    $hiddenUnreadB = (Read-Json (Invoke-Api 'GET' '/api/notifications/unread-count' -Token $tokenB)).count
    Assert-Stage8 'hiding reply hides descendants and related notifications' ($hideReply.Status -eq 200 -and $hiddenChild.Status -eq 404 -and $hiddenParentReply.Status -eq 409 -and $hiddenUnreadA -eq 0 -and $hiddenUnreadB -eq 0) "$($hideReply.Status)/$($hiddenChild.Status)/$($hiddenParentReply.Status)/$hiddenUnreadA/$hiddenUnreadB"

    $unhideReply = Invoke-Api 'PATCH' "/api/replies/$($reply.id)/status" -Token $tokenA -Body @{ status = 'visible' }
    $visibleChild = Invoke-Api 'GET' "/api/replies/$($childReply.id)"
    Assert-Stage8 'unhiding ancestor restores descendant visibility' ($unhideReply.Status -eq 200 -and $visibleChild.Status -eq 200) "$($unhideReply.Status)/$($visibleChild.Status)"
    $markAllRead = Invoke-Api 'POST' '/api/notifications/read-all' -Token $tokenB
    $unreadAfterReadAll = (Read-Json (Invoke-Api 'GET' '/api/notifications/unread-count' -Token $tokenB)).count
    Assert-Stage8 'mark all notifications read clears unread count' ($markAllRead.Status -eq 204 -and $unreadAfterReadAll -eq 0) "$($markAllRead.Status)/$unreadAfterReadAll"
    $deleteReplyBranch = Invoke-Api 'DELETE' "/api/replies/$($reply.id)" -Token $tokenA
    $deletedChild = Invoke-Api 'GET' "/api/replies/$($childReply.id)"
    Assert-Stage8 'deleting a parent reply cascades through its branch' ($deleteReplyBranch.Status -eq 204 -and $deletedChild.Status -eq 404) "$($deleteReplyBranch.Status)/$($deletedChild.Status)"

    $retainedReplyResponse = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = 'Retained after account deletion'; UseCapcode = 'false' })
    $reply = Read-Json $retainedReplyResponse
    Assert-Stage8 'create reply fixture for deleted-user retention' ($retainedReplyResponse.Status -eq 201) "$($retainedReplyResponse.Status)"

    $voteOne = Invoke-Api 'PUT' "/api/threads/$($thread.id)/vote" -Token $tokenB -Body @{ value = 1 }
    $voteTwo = Invoke-Api 'PUT' "/api/threads/$($thread.id)/vote" -Token $tokenB -Body @{ value = 1 }
    $voteOneScore = (Read-Json $voteOne).score
    $voteTwoScore = (Read-Json $voteTwo).score
    Assert-Stage8 'repeated vote is idempotent' ($voteOne.Status -eq 200 -and $voteTwo.Status -eq 200 -and $voteOneScore -eq $voteTwoScore) "$($voteOne.Status)/$($voteTwo.Status)"

    $adminLogin = Invoke-Api 'POST' '/api/auth/admin/login' -Body @{
        username = $settings.SeedSuperAdmin.Username; password = $settings.SeedSuperAdmin.Password
    }
    $rootToken = (Read-Json $adminLogin).accessToken
    $rootAdminQuery = [uri]::EscapeDataString($settings.SeedSuperAdmin.Username)
    $rootAdminList = Invoke-Api 'GET' "/api/admin/admins?q=$rootAdminQuery" -Token $rootToken
    $rootAdminId = (Read-Json $rootAdminList).items | Where-Object { $_.username -eq $settings.SeedSuperAdmin.Username } | Select-Object -ExpandProperty id -First 1
    $demoteLastSuperAdmin = Invoke-Api 'PUT' "/api/admin/admins/$rootAdminId" -Token $rootToken -Body @{ isSuperAdmin = $false }
    Assert-Stage8 'last super admin cannot be demoted' ($rootAdminList.Status -eq 200 -and $rootAdminId -gt 0 -and $demoteLastSuperAdmin.Status -eq 403) "$($rootAdminList.Status)/$($demoteLastSuperAdmin.Status)"
    $reservedBoard = Invoke-Api 'POST' '/api/admin/boards' -Token $rootToken -Body @{ slug = 'admin'; name = 'Reserved'; description = ''; sortOrder = 0 }
    $invalidBoardSlug = Invoke-Api 'POST' '/api/admin/boards' -Token $rootToken -Body @{ slug = 'bad_slug'; name = 'Invalid'; description = ''; sortOrder = 0 }
    $duplicateBoardSlug = Invoke-Api 'POST' '/api/admin/boards' -Token $rootToken -Body @{ slug = 'tech'; name = 'Duplicate'; description = ''; sortOrder = 0 }
    Assert-Stage8 'reserved, malformed, and duplicate board slugs rejected' ($reservedBoard.Status -eq 400 -and $invalidBoardSlug.Status -eq 400 -and $duplicateBoardSlug.Status -eq 409) "$($reservedBoard.Status)/$($invalidBoardSlug.Status)/$($duplicateBoardSlug.Status)"
    $temporaryBoard = Invoke-Api 'POST' '/api/admin/boards' -Token $rootToken -Body @{ slug = "e2e-slug-$stamp"; name = 'Temporary Slug'; description = ''; sortOrder = 99 }
    if ($temporaryBoard.Status -eq 201) { $temporaryBoardId = (Read-Json $temporaryBoard).id }
    $slugUpdate = if ($temporaryBoardId) { Invoke-Api 'PUT' "/api/admin/boards/$temporaryBoardId" -Token $rootToken -Body @{ slug = "e2e-renamed-$stamp"; name = 'Temporary Slug Updated'; description = ''; sortOrder = 99 } } else { $null }
    $slugAfterUpdate = if ($temporaryBoardId) { Invoke-Api 'GET' "/api/admin/boards/$temporaryBoardId" -Token $rootToken } else { $null }
    Assert-Stage8 'board slug remains immutable after update' ($temporaryBoard.Status -eq 201 -and $slugUpdate.Status -eq 200 -and (Read-Json $slugAfterUpdate).slug -eq "e2e-slug-$stamp") "$($temporaryBoard.Status)/$($slugUpdate.Status)"
    if ($temporaryBoardId) { $null = Invoke-Api 'DELETE' "/api/admin/boards/$temporaryBoardId" -Token $rootToken; $temporaryBoardId = $null }
    $adminVote = Invoke-Api 'PUT' "/api/threads/$($thread.id)/vote" -Token $rootToken -Body @{ value = 1 }
    Assert-Stage8 'admin vote rejected' ($adminLogin.Status -eq 200 -and $adminVote.Status -eq 403) "$($adminLogin.Status)/$($adminVote.Status)"

    $noCapcodeAdminForm = New-Form @{ BoardSlug = 'tech'; Title = "E2E admin private $stamp"; Content = 'No capcode should not expose admin identity'; UseCapcode = 'false' }
    $noCapcodeAdminThread = Invoke-Api 'POST' '/api/admin/threads' -Token $rootToken -Content $noCapcodeAdminForm
    $noCapcodeDto = Read-Json $noCapcodeAdminThread
    if ($noCapcodeAdminThread.Status -eq 201) { $threadIds.Add([int]$noCapcodeDto.id) }
    $noCapcodePublic = if ($noCapcodeDto) { Invoke-Api 'GET' "/api/threads/$($noCapcodeDto.id)" } else { $null }
    Assert-Stage8 'admin without capcode is anonymous in public DTO' ($noCapcodeAdminThread.Status -eq 201 -and $noCapcodePublic.Status -eq 200 -and $noCapcodePublic.Body -notmatch 'superadmin|author_user_id|author_admin_id') "$($noCapcodeAdminThread.Status)/$($noCapcodePublic.Status)"

    $temporaryAdminName = "e2e_admin_$stamp"
    $temporaryAdminEmail = "$temporaryAdminName@funforum.local"
    $temporaryAdminPassword = 'E2E!TemporaryAdmin_2026'
    $createAdmin = Invoke-Api 'POST' '/api/admin/admins' -Token $rootToken -Body @{
        username = $temporaryAdminName; email = $temporaryAdminEmail; password = $temporaryAdminPassword; isSuperAdmin = $false
    }
    if ($createAdmin.Status -eq 201) { $temporaryAdminId = (Read-Json $createAdmin).id }
    $temporaryLogin = if ($temporaryAdminId) {
        Invoke-Api 'POST' '/api/auth/admin/login' -Body @{ username = $temporaryAdminName; password = $temporaryAdminPassword }
    } else { $null }
    $temporaryAdminToken = if ($temporaryLogin) { (Read-Json $temporaryLogin).accessToken } else { $null }
    $capcodeAdminThread = if ($temporaryAdminToken) {
        Invoke-Api 'POST' '/api/admin/threads' -Token $temporaryAdminToken -Content (New-Form @{ BoardSlug = 'tech'; Title = "E2E deleted admin $stamp"; Content = 'Capcode remains after account removal'; UseCapcode = 'true' })
    } else { $null }
    $capcodeAdminDto = if ($capcodeAdminThread) { Read-Json $capcodeAdminThread } else { $null }
    if ($capcodeAdminDto) { $threadIds.Add([int]$capcodeAdminDto.id) }
    $deleteTemporaryAdmin = if ($temporaryAdminId) { Invoke-Api 'DELETE' "/api/admin/admins/$temporaryAdminId" -Token $rootToken } else { $null }
    $retainedAdminPost = if ($capcodeAdminDto) { Invoke-Api 'GET' "/api/threads/$($capcodeAdminDto.id)" } else { $null }
    $retainedAdminDto = if ($retainedAdminPost) { Read-Json $retainedAdminPost } else { $null }
    Assert-Stage8 'deleting admin retains post and capcode' ($createAdmin.Status -eq 201 -and $capcodeAdminThread.Status -eq 201 -and $deleteTemporaryAdmin.Status -eq 204 -and $retainedAdminPost.Status -eq 200 -and $retainedAdminDto.capcode -eq 'admin') "$($createAdmin.Status)/$($capcodeAdminThread.Status)/$($deleteTemporaryAdmin.Status)/$($retainedAdminPost.Status)"
    $temporaryAdminId = $null

    $lateReply = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = 'Move thread to latest'; UseCapcode = 'false' })
    $lateReplyId = (Read-Json $lateReply).id
    $adminHideReply = Invoke-Api 'PATCH' "/api/replies/$lateReplyId/status" -Token $rootToken -Body @{ status = 'hidden' }
    $publicHiddenReply = Invoke-Api 'GET' "/api/replies/$lateReplyId"
    $ownerHiddenReply = Invoke-Api 'GET' "/api/replies/$lateReplyId" -Token $tokenA
    $authorHiddenReply = Invoke-Api 'GET' "/api/replies/$lateReplyId" -Token $tokenB
    $adminHiddenReply = Invoke-Api 'GET' "/api/replies/$lateReplyId" -Token $rootToken
    $ownerCannotUnhideAdminReply = Invoke-Api 'PATCH' "/api/replies/$lateReplyId/status" -Token $tokenA -Body @{ status = 'visible' }
    $adminUnhideReply = Invoke-Api 'PATCH' "/api/replies/$lateReplyId/status" -Token $rootToken -Body @{ status = 'visible' }
    Assert-Stage8 'admin-hidden reply is private and only admin may unhide' ($adminHideReply.Status -eq 200 -and $publicHiddenReply.Status -eq 404 -and $ownerHiddenReply.Status -eq 200 -and $authorHiddenReply.Status -eq 200 -and $adminHiddenReply.Status -eq 200 -and $ownerCannotUnhideAdminReply.Status -eq 403 -and $adminUnhideReply.Status -eq 200) "$($adminHideReply.Status)/$($publicHiddenReply.Status)/$($ownerHiddenReply.Status)/$($authorHiddenReply.Status)/$($adminHiddenReply.Status)/$($ownerCannotUnhideAdminReply.Status)/$($adminUnhideReply.Status)"
    $lastReplyBeforeEdit = (Read-Json (Invoke-Api 'GET' "/api/threads/$($thread.id)")).lastReplyAt
    $editThread = Invoke-Api 'PUT' "/api/threads/$($thread.id)" -Token $tokenA -Body @{ title = "E2E edited $stamp"; content = 'Editing should not bump latest activity' }
    $lastReplyAfterEdit = (Read-Json (Invoke-Api 'GET' "/api/threads/$($thread.id)")).lastReplyAt
    Assert-Stage8 'editing thread does not change lastReplyAt' ($editThread.Status -eq 200 -and $lastReplyBeforeEdit -eq $lastReplyAfterEdit) "$($editThread.Status)"
    $topPage1 = Invoke-Api 'GET' '/api/boards/tech/threads?sort=top&page=1&pageSize=1'
    $topPage2 = Invoke-Api 'GET' '/api/boards/tech/threads?sort=top&page=2&pageSize=1'
    $topPage3 = Invoke-Api 'GET' '/api/boards/tech/threads?sort=top&page=3&pageSize=1'
    $topIds = @((Read-Json $topPage1).items[0].id, (Read-Json $topPage2).items[0].id, (Read-Json $topPage3).items[0].id)
    $topStable = $topIds.Count -eq 3 -and @($topIds | Select-Object -Unique).Count -eq 3 -and $topIds[1] -gt $topIds[2]
    $latest = Invoke-Api 'GET' '/api/boards/tech/threads?sort=latest&page=1&pageSize=1'
    $latestId = (Read-Json $latest).items[0].id
    Assert-Stage8 'equal-score pagination is stable and latest reply bumps thread' ($lateReply.Status -eq 201 -and $topPage1.Status -eq 200 -and $topPage2.Status -eq 200 -and $topPage3.Status -eq 200 -and $topStable -and $latestId -eq $thread.id) "$($topPage1.Status)/$($topPage2.Status)/$($topPage3.Status); latest=$latestId"

    $hide = Invoke-Api 'PATCH' "/api/admin/threads/$($thread.id)/status" -Token $rootToken -Body @{ status = 'hidden' }
    $techBoardList = Invoke-Api 'GET' '/api/admin/boards?page=1&pageSize=50' -Token $rootToken
    $techBoardId = ((Read-Json $techBoardList).items | Where-Object { $_.slug -eq 'tech' }).id
    $deleteBoardWithHiddenThread = Invoke-Api 'DELETE' "/api/admin/boards/$techBoardId" -Token $rootToken
    Assert-Stage8 'hidden thread still blocks board deletion' ($hide.Status -eq 200 -and $deleteBoardWithHiddenThread.Status -eq 409) "$($hide.Status)/$($deleteBoardWithHiddenThread.Status)"
    $hiddenGet = Invoke-Api 'GET' "/api/threads/$($thread.id)"
    $replyHiddenThread = Invoke-Api 'POST' "/api/threads/$($thread.id)/replies" -Token $tokenA -Content (New-Form @{ Content = 'Cannot reply'; UseCapcode = 'false' })
    Assert-Stage8 'hidden thread is 404 and rejects reply' ($hide.Status -eq 200 -and $hiddenGet.Status -eq 404 -and $replyHiddenThread.Status -eq 409) "$($hide.Status)/$($hiddenGet.Status)/$($replyHiddenThread.Status)"
    $unhide = Invoke-Api 'PATCH' "/api/admin/threads/$($thread.id)/status" -Token $rootToken -Body @{ status = 'visible' }
    Assert-Stage8 'admin restores hidden thread' ($unhide.Status -eq 200) "$($unhide.Status)"

    if ($IncludeBoundaryStress) {
        $replyLimitThreadResponse = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content (New-Form @{ Title = "E2E 500-reply cap $stamp"; Content = 'Boundary fixture'; UseCapcode = 'false' })
        $replyLimitThread = Read-Json $replyLimitThreadResponse
        if ($replyLimitThreadResponse.Status -eq 201) { $threadIds.Add([int]$replyLimitThread.id) }
        $firstLimitReplyId = $null
        $createdLimitReplies = 0
        if ($replyLimitThreadResponse.Status -eq 201) {
            for ($index = 0; $index -lt 500; $index++) {
                $limitReply = Invoke-Api 'POST' "/api/threads/$($replyLimitThread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = "Boundary reply $index"; UseCapcode = 'false' })
                if ($limitReply.Status -ne 201) { break }
                if ($index -eq 0) { $firstLimitReplyId = (Read-Json $limitReply).id }
                $createdLimitReplies++
            }
        }
        $replyAtLimit = if ($replyLimitThreadResponse.Status -eq 201) { Invoke-Api 'POST' "/api/threads/$($replyLimitThread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = 'Over reply limit'; UseCapcode = 'false' }) } else { $null }
        $deleteLimitReply = if ($firstLimitReplyId) { Invoke-Api 'DELETE' "/api/replies/$firstLimitReplyId" -Token $tokenB } else { $null }
        $replyAfterDelete = if ($deleteLimitReply -and $deleteLimitReply.Status -eq 204) { Invoke-Api 'POST' "/api/threads/$($replyLimitThread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = 'Allowed after deleting one'; UseCapcode = 'false' }) } else { $null }
        Assert-Stage8 '500 replies rejected, then allowed after deleting one' ($createdLimitReplies -eq 500 -and $replyAtLimit.Status -eq 409 -and $deleteLimitReply.Status -eq 204 -and $replyAfterDelete.Status -eq 201) "created=$createdLimitReplies; over=$($replyAtLimit.Status); after-delete=$($replyAfterDelete.Status)"

        $imageLimitThreadResponse = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content (New-Form @{ Title = "E2E 150-image cap $stamp"; Content = 'Boundary fixture'; UseCapcode = 'false' })
        $imageLimitThread = Read-Json $imageLimitThreadResponse
        if ($imageLimitThreadResponse.Status -eq 201) { $threadIds.Add([int]$imageLimitThread.id) }
        $createdImageReplies = 0
        if ($imageLimitThreadResponse.Status -eq 201) {
            for ($index = 0; $index -lt 150; $index++) {
                $imageForm = New-Form @{ Content = ''; UseCapcode = 'false' }
                Add-Image $imageForm $gifBytes "limit-$index.gif" 'image/gif'
                $imageReply = Invoke-Api 'POST' "/api/threads/$($imageLimitThread.id)/replies" -Token $tokenB -Content $imageForm
                if ($imageReply.Status -ne 201) { break }
                $createdImageReplies++
            }
        }
        $imageAtLimitForm = New-Form @{ Content = ''; UseCapcode = 'false' }
        Add-Image $imageAtLimitForm $gifBytes 'over-limit.gif' 'image/gif'
        $imageAtLimit = if ($imageLimitThreadResponse.Status -eq 201) { Invoke-Api 'POST' "/api/threads/$($imageLimitThread.id)/replies" -Token $tokenB -Content $imageAtLimitForm } else { $null }
        $textAtImageLimit = if ($imageAtLimit.Status -eq 409) { Invoke-Api 'POST' "/api/threads/$($imageLimitThread.id)/replies" -Token $tokenB -Content (New-Form @{ Content = 'Text still allowed'; UseCapcode = 'false' }) } else { $null }
        Assert-Stage8 '150 images rejected but text reply remains allowed' ($createdImageReplies -eq 150 -and $imageAtLimit.Status -eq 409 -and $textAtImageLimit.Status -eq 201) "images=$createdImageReplies; over=$($imageAtLimit.Status); text=$($textAtImageLimit.Status)"
    }

    $fakeImageForm = New-Form @{ Title = 'E2E fake image'; Content = 'Reject fake type'; UseCapcode = 'false' }
    Add-Image $fakeImageForm ([System.Text.Encoding]::UTF8.GetBytes('<?php echo 1; ?>')) 'fake.jpg' 'image/jpeg'
    $fakeImage = Invoke-Api 'POST' '/api/boards/tech/threads' -Token $tokenA -Content $fakeImageForm
    Assert-Stage8 'PHP payload renamed to jpg rejected' ($fakeImage.Status -eq 400) "$($fakeImage.Status)"

    $deletedUser = Invoke-Api 'DELETE' "/api/admin/users/$userBId" -Token $rootToken
    $retainedReply = Invoke-Api 'GET' "/api/replies/$($reply.id)"
    $retainedThread = Invoke-Api 'GET' "/api/threads/$($thread.id)"
    Assert-Stage8 'deleting user retains content with deleted author' ($deletedUser.Status -eq 204 -and $retainedReply.Status -eq 200 -and (Read-Json $retainedReply).authorName -eq '[deleted]' -and $retainedThread.Status -eq 200) "$($deletedUser.Status)/$($retainedReply.Status)/$($retainedThread.Status)"
    $userBId = $null
}
catch {
    $script:failures++
    Write-Output "FAIL unexpected test error: $($_.Exception.Message)"
}
finally {
    if ($rootToken) {
        foreach ($id in $threadIds) {
            try { $null = Invoke-Api 'DELETE' "/api/admin/threads/$id" -Token $rootToken } catch { }
        }
        if ($temporaryBoardId) { try { $null = Invoke-Api 'DELETE' "/api/admin/boards/$temporaryBoardId" -Token $rootToken } catch { } }
        if ($uploadedAttachmentPath) {
            try {
                $attachment = Invoke-Api 'GET' $uploadedAttachmentPath
                Assert-Stage8 'thread deletion removes uploaded attachment file' ($attachment.Status -eq 404) "$($attachment.Status)"
            }
            catch {
                Assert-Stage8 'thread deletion removes uploaded attachment file' $false $_.Exception.Message
            }
        }
        if ($userAId) { try { $null = Invoke-Api 'DELETE' "/api/admin/users/$userAId" -Token $rootToken } catch { } }
        if ($userBId) { try { $null = Invoke-Api 'DELETE' "/api/admin/users/$userBId" -Token $rootToken } catch { } }
        if ($temporaryAdminId) { try { $null = Invoke-Api 'DELETE' "/api/admin/admins/$temporaryAdminId" -Token $rootToken } catch { } }
    }
    $client.Dispose()
}

Write-Output "STAGE8_SMOKE_FAILURES=$script:failures"
if ($script:failures -gt 0) { exit 1 }