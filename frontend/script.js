let selectedReceiver = null;
let currentChatRoomId = null;
const backendUrl = "https://localhost:7030";

// Toast Bildirim Fonksiyonu
function showToast(message, type = 'info') {
  const toastContainer = document.getElementById('toastContainer');
  if (!toastContainer) return; // Eğer container yoksa işlem yapma
  const toast = document.createElement('div');
  const toastClass = type === 'success' ? 'bg-success' : (type === 'error' ? 'bg-danger' : 'bg-primary');
  
  toast.className = `toast show align-items-center text-white ${toastClass} border-0 mb-2`;
  toast.setAttribute('role', 'alert');
  toast.setAttribute('aria-live', 'assertive');
  toast.setAttribute('aria-atomic', 'true');

  toast.innerHTML = `
    <div class="d-flex">
      <div class="toast-body">${message}</div>
      <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
    </div>
  `;
  
  toastContainer.appendChild(toast);

  // 3 saniye sonra tostu kaldır
  setTimeout(() => {
    // Bootstrap 5'in Toast bileşenini manuel olarak gizle
    const bsToast = bootstrap.Toast.getInstance(toast);
    if (bsToast) {
        bsToast.hide();
    }
    // DOM'dan kaldırmak için
    setTimeout(() => toast.remove(), 500);
  }, 3000);
}

// Eğer giriş yapılmamışsa sahte kullanıcı ile devam et
const fallbackUser = JSON.parse(localStorage.getItem("user")) || {
    name: "Ziyaretçi",
    email: "anonim@example.com",
    gender: "unknown",
    avatar: ""
};

// Bu fonksiyon sohbet sayfasında çalışır
function loadUser() {

  const token = localStorage.getItem("token");
  if (!token) {
    window.location.href = "index.html";
    return;
  }

  fetch(`${backendUrl}/api/User/profile`, {
    headers: { Authorization: "Bearer " + token }
  })
    .then(res => {
      if (!res.ok) throw new Error("Token geçersiz");
      return res.json();
    })
    .then(data => {
      console.log("Kullanıcı:", data); // ✅ burada email: xxx döner
      // İstersen localStorage.setItem("userEmail", data.email); şeklinde de saklayabilirsin
    })
    .catch(() => {
      localStorage.removeItem("token");
      window.location.href = "index.html";
    });

  const userData = localStorage.getItem("chitchat_user");
if (!userData) {
  window.location.href = "index.html";
  return;
}
const user = JSON.parse(userData);

  $("#profileName").text(user.name);
  $("#profileGender").text(user.gender === "male" ? "Erkek" : "Kadın");
 const avatarPath = user.avatar || (user.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
$("#profileAvatar").attr("src", avatarPath);
 $("#btnProfile").click(function () {
  $("#chatSection").removeClass("active").hide();
  $("#profileSection").addClass("active").fadeIn(200);
  $("#btnChat").removeClass("active");
  $(this).addClass("active");
});

$("#btnChat").click(function () {
  $("#profileSection").removeClass("active").hide();
  $("#chatSection").addClass("active").fadeIn(200);
  $("#btnProfile").removeClass("active");
  $(this).addClass("active");
});

renderUserList(); // kullanıcıları yükle
  loadMessages();
  // Kullanıcı listesini göster

  fetchAndStoreFriends();
}

// Arkadaş listesini API'dan çek
async function fetchFriendsFromAPI() {
  const token = localStorage.getItem("token");
  if (!token) return [];
  try {
    const res = await fetch(`${backendUrl}/api/user/friends`, {
      headers: { Authorization: "Bearer " + token }
    });
    if (!res.ok) throw new Error("Arkadaşlar alınamadı");
    const friends = await res.json();
    // Backend'den gelen isimleri frontend ile uyumlu hale getir
    const mapped = friends.map(f => ({
      id: f.id || f.Id || f.userId || f.UserId, // userId'yi de al
      displayName: f.displayName || f.userName || f.email,
      userName: f.userName || f.displayName || f.email,
      email: f.email,
      avatar: f.profilePicture || "",
      gender: f.gender || "unknown"
    }));
    window.chitchatFriends = mapped; // Arkadaşları window'da sakla
    return mapped;
  } catch (err) {
    window.chitchatFriends = [];
    return [];
  }
}

// Kullanıcı cache'i (id -> user objesi)
window.chitchatUserCache = {};

// Gönderenin en iyi adını bul
async function getBestUserName(sender) {
  if (typeof sender === 'object' && sender !== null) {
    if (sender.displayName && sender.displayName !== 'Bilinmeyen') return sender.displayName;
    if (sender.userName && sender.userName !== 'Bilinmeyen') return sender.userName;
    if (sender.email) return sender.email;
    if (sender.id) {
      // Önce arkadaş listesinde ara
      const friends = window.chitchatFriends || [];
      const found = friends.find(f => f.id === sender.id);
      if (found) return found.displayName || found.userName || found.email;
      // Sonra cache'de ara
      if (window.chitchatUserCache[sender.id]) {
        const u = window.chitchatUserCache[sender.id];
        return u.displayName || u.userName || u.email || `Kullanıcı (id: ${sender.id})`;
      }
      // API'dan çek (ilk defa ise)
      try {
        const token = localStorage.getItem("token");
        const res = await fetch(`${backendUrl}/api/users/${sender.id}`, {
          headers: { Authorization: "Bearer " + token }
        });
        if (res.ok) {
          const u = await res.json();
          window.chitchatUserCache[sender.id] = u;
          return u.displayName || u.userName || u.email || `Kullanıcı (id: ${sender.id})`;
        }
      } catch {}
      return `Kullanıcı (id: ${sender.id})`;
    }
  } else if (typeof sender === 'string') {
    if (sender && sender !== 'Bilinmeyen') return sender;
    // Eğer string id ise, arkadaş listesinde ara
    const friends = window.chitchatFriends || [];
    const found = friends.find(f => f.id === sender);
    if (found) return found.displayName || found.userName || found.email;
    if (window.chitchatUserCache[sender]) {
      const u = window.chitchatUserCache[sender];
      return u.displayName || u.userName || u.email || `Kullanıcı (id: ${sender})`;
    }
    // API'dan çek
    try {
      const token = localStorage.getItem("token");
      const res = await fetch(`${backendUrl}/api/users/${sender}`, {
        headers: { Authorization: "Bearer " + token }
      });
      if (res.ok) {
        const u = await res.json();
        window.chitchatUserCache[sender] = u;
        return u.displayName || u.userName || u.email || `Kullanıcı (id: ${sender})`;
      }
    } catch {}
    return `Kullanıcı (id: ${sender})`;
  }
  return 'Kullanıcı';
}

// Arkadaş listesini göster
async function renderUserList(filter = "") {
  const friends = await fetchFriendsFromAPI();
  const groups = []; // Gruplar için ayrı bir API varsa buradan çekilebilir
  const filtered = friends.filter(u =>
    (u.displayName || u.userName || u.email || '').toLowerCase().includes((filter || '').toLowerCase())
  );
  // Engellenenler localStorage'da tutuluyorsa burada kalabilir
  const blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
  const visible = filtered.filter(u => !blocked.includes(u.email));
  let html = "";
  if (visible.length === 0) {
    html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
  } else {
    visible.forEach(u => {
      const avatar = u.avatar || (u.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
      const displayName = u.displayName || u.userName || u.email || u.id || 'Kullanıcı';
      html += `
        <li class="list-group-item d-flex align-items-center user-item" data-name="${displayName}" data-email="${u.email}" data-userid="${u.id}">
          <img src="${avatar}" class="rounded-circle me-2" style="width:30px; height:30px;">
          ${displayName}
          <button class="btn btn-sm btn-outline-danger ms-auto" onclick="blockUser('${u.email}')">Engelle</button>
        </li>
      `;
    });
  }
  // Grupları ekle (varsa)
  groups.forEach(g => {
    html += `
      <li class="list-group-item d-flex align-items-center user-item group-item" data-name="${g.name}" data-email="group:${g.name}">
        <strong>👥 ${g.name}</strong>
      </li>
    `;
  });
  document.getElementById("userList").innerHTML = html;
  document.querySelectorAll(".user-item").forEach(item => {
    item.addEventListener("click", async () => {
      document.querySelectorAll(".user-item").forEach(i => i.classList.remove("active-user"));
      item.classList.add("active-user");
      selectedReceiver = {
        name: item.dataset.name,
        email: item.dataset.email,
        id: item.dataset.userid
      };
      // Oda ID'sini bul veya oluştur
      if (!item.classList.contains("group-item")) {
        // Birebir sohbet için yeni endpoint ile oda oluştur veya bul
        const token = localStorage.getItem("token");
        try {
          const res = await fetch(`${backendUrl}/api/chat/direct-room`, {
            method: "POST",
            headers: {
              "Content-Type": "application/json",
              Authorization: "Bearer " + token
            },
            body: JSON.stringify({ OtherUserId: selectedReceiver.id })
          });
          if (res.ok) {
            const room = await res.json();
            currentChatRoomId = room.id;
          } else {
            currentChatRoomId = null;
          }
        } catch {
          currentChatRoomId = null;
        }
      } else {
        // Grup sohbeti için
        currentChatRoomId = selectedReceiver.name;
      }
      if (item.classList.contains("group-item")) {
        document.querySelector("#chatSection .card-header").innerText = `👥 Grup: ${selectedReceiver.name}`;
      } else {
        document.querySelector("#chatSection .card-header").innerText = `${selectedReceiver.name} ile Sohbet`;
      }
      loadMessages();
    });
  });
}

// Grup kurma modalında arkadaşları göster
async function populateFriendCheckboxes() {
  const friends = await fetchFriendsFromAPI();
  const container = document.getElementById("friendCheckboxes");
  container.innerHTML = "";
  if (friends.length === 0) {
    container.innerHTML = `<div class="text-muted">Hiç arkadaşınız yok.</div>`;
    return;
  }
  friends.forEach(friend => {
    container.innerHTML += `
      <div class="form-check">
        <input class="form-check-input" type="checkbox" value="${friend.email}" id="friend-${friend.email}">
        <label class="form-check-label" for="friend-${friend.email}">
          ${friend.name} (${friend.email})
        </label>
      </div>
    `;
  });
}

// fetchAndStoreFriends fonksiyonu artık sadece renderUserList'i tetikler
async function fetchAndStoreFriends() {
  await renderUserList();
}

async function blockUser(email) {
  const token = localStorage.getItem("token");
  if (!confirm(`🚫 ${email} adresli kullanıcıyı engellemek istediğinize emin misiniz?`)) return;

  try {
    const res = await fetch(`${backendUrl}/api/users/block`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      body: JSON.stringify({ emailToBlock: email }),
    });
    if (!res.ok) throw new Error("Kullanıcı engellenemedi.");
    showToast("🚫 Kullanıcı engellendi.", 'success');
    renderUserList();
    renderBlockedUsers();
  } catch (err) {
    showToast("❌ Hata: " + err.message, 'error');
  }
}

async function renderBlockedUsers() {
  const token = localStorage.getItem("token");
  const list = document.getElementById("blockedList");
  if (!list) return;

  try {
    const res = await fetch(`${backendUrl}/api/users/blocked`, {
      headers: { Authorization: "Bearer " + token },
    });
    if (!res.ok) throw new Error("Engellenenler listesi alınamadı.");
    
    const blockedUsers = await res.json();
    list.innerHTML = "";

    if (blockedUsers.length === 0) {
      list.innerHTML = `<li class="list-group-item text-muted">Engellenmiş kullanıcı yok</li>`;
      return;
    }

    blockedUsers.forEach(user => {
      const item = document.createElement("li");
      item.className = "list-group-item d-flex justify-content-between align-items-center";
      item.innerHTML = `
        <span>${user.name} (${user.email})</span>
        <button class="btn btn-sm btn-outline-secondary" onclick="unblockUser('${user.email}')">Kaldır</button>
      `;
      list.appendChild(item);
    });
  } catch(err) {
    list.innerHTML = `<li class="list-group-item text-danger">${err.message}</li>`;
  }
}

async function unblockUser(email) {
  const token = localStorage.getItem("token");
  try {
    const res = await fetch(`${backendUrl}/api/users/unblock`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      body: JSON.stringify({ emailToUnblock: email }),
    });
    if (!res.ok) throw new Error("Engelleme kaldırılamadı.");
    showToast("✅ Kullanıcının engeli kaldırıldı.", 'success');
    renderUserList();
    renderBlockedUsers();
  } catch (err) {
    showToast("❌ Hata: " + err.message, 'error');
  }
}


// Arama kutusu olayı
document.addEventListener("DOMContentLoaded", () => {
  const searchInput = document.getElementById("userSearch");
  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      renderUserList(e.target.value);
    });
  } 
});

// MESAJ GÖNDERME (API ENTEGRASYONU)
// ✅ SignalR Entegreli sendMessage() fonksiyonu
async function sendMessage() {
  const token = localStorage.getItem("token");
  if (!token) return showToast("Giriş yapmalısınız!", 'error');
  if (!selectedReceiver) return showToast("Lütfen bir kişi veya grup seçin.", 'error');

  const messageInput = document.getElementById("messageInput");
  const fileInput = document.getElementById("fileInput");
  const content = messageInput.value.trim();
  const file = fileInput.files[0];

  if (!content && !file) return; // Boş mesaj veya dosya gönderme

  let chatRoomId = currentChatRoomId;
  if (!chatRoomId) {
    return showToast("Oda bulunamadı. Lütfen sohbeti tekrar seçin.", 'error');
  }

  // 1. Önce metin mesajını gönder
  let messageId = null;
  try {
    const res = await fetch(`${backendUrl}/api/message/send`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify({
        chatRoomId: chatRoomId,
        content: content,
        attachmentUrls: []
      })
    });
    if (!res.ok) throw new Error("Mesaj gönderilemedi");
    const message = await res.json();
    messageId = message.id || message.Id || message._id;
  } catch (err) {
    showToast("❌ Mesaj gönderilemedi: " + err.message, 'error');
    return;
  }

  // 2. Dosya varsa upload et
  if (file && messageId) {
    const formData = new FormData();
    formData.append("file", file);
    try {
      const res = await fetch(`${backendUrl}/api/file/upload?messageId=${messageId}`, {
        method: "POST",
        headers: {
          Authorization: "Bearer " + token
        },
        body: formData
      });
      if (!res.ok) throw new Error("Dosya yüklenemedi");
      // Dosya başarıyla yüklendi
    } catch (err) {
      showToast("❌ Dosya yüklenemedi: " + err.message, 'error');
      // Dosya yüklenemese bile mesaj gönderildiği için devam edelim
    }
  }

  // 3. SignalR ile real-time gönderim (YENİ EKLENEN)
 const currentUser = JSON.parse(localStorage.getItem("chitchat_user") || "{}");
if (content) {
  await addMessageToChat(
    currentUser.name || currentUser.displayName || currentUser.email, 
    content, 
    new Date()
  );
}

// 4. SignalR ile real-time gönderim (başkaları görsün diye)
if (isSignalRConnected && signalRConnection) {
  try {
    await signalRConnection.invoke("SendMessage", chatRoomId, content);
    console.log("SignalR message sent successfully");
  } catch (signalRError) {
    console.error("SignalR send error:", signalRError);
  }
}

// 5. Input'ları temizle
messageInput.value = "";
fileInput.value = "";
}

// ✅ SignalR Entegreli loadMessages() fonksiyonu
async function loadMessages() {
  const token = localStorage.getItem("token");
  if (!token) return;
  if (!selectedReceiver) return;
  let chatRoomId = currentChatRoomId;
  if (!chatRoomId) {
    document.getElementById("messageArea").innerHTML = `<div class='text-danger'>Oda bulunamadı.</div>`;
    return;
  }
  let endpoint = `${backendUrl}/api/message/chat/${chatRoomId}`;
  try {
    const res = await fetch(endpoint, {
      headers: { Authorization: "Bearer " + token }
    });
    if (!res.ok) throw new Error("Mesajlar alınamadı");
    const messages = await res.json();
    renderMessages(messages);
    // SignalR ile chat room'a katıl
    if (isSignalRConnected && signalRConnection) {
      try {
        if (window.currentChatRoom && window.currentChatRoom !== chatRoomId) {
          await signalRConnection.invoke("LeaveChatRoom", window.currentChatRoom);
          console.log(`Left previous room: ${window.currentChatRoom}`);
        }
        await signalRConnection.invoke("JoinChatRoom", chatRoomId);
        window.currentChatRoom = chatRoomId;
        console.log(`Joined room: ${chatRoomId}`);
        updateConnectionStatus("Bağlı - " + selectedReceiver.name, "success");
      } catch (signalRError) {
        console.error("SignalR room join error:", signalRError);
        updateConnectionStatus("Bağlı (Room Hatası)", "warning");
      }
    }
  } catch (err) {
    document.getElementById("messageArea").innerHTML = `<div class='text-danger'>${err.message}</div>`;
  }
}

// ✅ YENİ EKLENEN: Real-time mesajı chat'e eklemek için renderMessages'ı güncelle
async function renderMessages(messages) {
  const area = document.getElementById("messageArea");
  area.innerHTML = "";
  
  if (!messages || messages.length === 0) {
    area.innerHTML = `<div class='text-muted'>Hiç mesaj yok.</div>`;
    return;
  }
  
  for (const msg of messages) {
    let senderName = await getBestUserName(msg.sender) || await getBestUserName(msg.senderName);
    await addMessageToChat(senderName, msg.content || msg.message, msg.timestamp || msg.createdAt || new Date());
  }
  
  area.scrollTop = area.scrollHeight;
}

// addMessageToChat fonksiyonunu async yap
async function addMessageToChat(sender, content, timestamp) {
  const messageArea = document.getElementById("messageArea");
  let senderName = await getBestUserName(sender);
  const time = new Date(timestamp).toLocaleTimeString('tr-TR', {
    hour: '2-digit', 
    minute: '2-digit'
  });
  const currentUser = JSON.parse(localStorage.getItem("chitchat_user") || "{}");
  const isOwnMessage = senderName === currentUser.name || senderName === currentUser.displayName || senderName === currentUser.userName || senderName === currentUser.email;
  const alignClass = isOwnMessage ? 'ms-auto text-end' : 'me-auto text-start';
  const bubbleClass = isOwnMessage ? 'bg-primary text-white' : 'bg-light border';
  const messageDiv = document.createElement('div');
  messageDiv.className = `mb-2 d-flex flex-column ${alignClass}`;
  messageDiv.innerHTML = `
    <div class=\"p-2 rounded ${bubbleClass}\" style=\"max-width: 70%; display: inline-block; word-break: break-word;\">
      ${!isOwnMessage && senderName !== 'Kullanıcı' ? `<strong>${senderName}</strong><br>` : ''}
      ${content}
      <div class=\"small mt-1 ${isOwnMessage ? 'text-light' : 'text-muted'}\">${time}</div>
    </div>
  `;
  messageArea.appendChild(messageDiv);
  messageArea.scrollTop = messageArea.scrollHeight;
}

// ✅ YENİ EKLENEN: chat.html'deki addRealTimeMessage fonksiyonunu güncelle
// Bu fonksiyon chat.html'de SignalR event'inde çağrılacak
async function addRealTimeMessage(message) {
  let senderName = await getBestUserName(message.sender) || await getBestUserName(message.senderName);
  await addMessageToChat(senderName, message.content, message.timestamp);
  const currentUser = JSON.parse(localStorage.getItem("chitchat_user") || "{}");
  if (senderName !== currentUser.name && senderName !== currentUser.displayName && senderName !== currentUser.userName && senderName !== currentUser.email) {
    showNotification(`${senderName}: ${message.content}`);
  }
}

// Sohbet sayfası açıldığında çalışır
window.onload = function () {
  //const token = localStorage.getItem("token");
  const user = localStorage.getItem("chitchat_user");
   if (user) {
    const navUser = document.getElementById("navUserName");
    if (navUser) {
      navUser.innerText = `👋 ${user.name}`;
    }
  }

 // if (!token || !user) {
    // Token veya kullanıcı bilgisi yoksa giriş ekranına gönder
    //window.location.href = "index.html";
   // return;
  //}

  if (window.location.pathname.includes("chat.html")) {
    loadUser();
    document.getElementById("btnChat").click();
  }
   const searchInput = document.getElementById("userSearch");
  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      renderUserList(e.target.value);
    });
  }
};
async function logout() {
  const token = localStorage.getItem("token");
  try {
    if (token) {
      await fetch(`${backendUrl}/api/user/logout`, {
        method: "POST",
        headers: { Authorization: "Bearer " + token }
      });
    }
  } catch (err) { /* sessizce geç */ }
  localStorage.removeItem("token");
  window.location.href = "index.html";
}
async function addFriend() {
  const email = document.getElementById("friendEmail").value.trim();
  if (!email) return showToast("E-posta girin", 'error');
  const token = localStorage.getItem("token");
  try {
    const res = await fetch(`${backendUrl}/api/user/friends/request`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify({ FriendEmail: email })
    });
    if (!res.ok) {
      const err = await res.json();
      throw new Error(err.error || "İstek gönderilemedi");
    }
    showToast("Arkadaşlık isteği gönderildi", 'success');
    document.getElementById("friendEmail").value = "";
  } catch (err) {
    showToast("Hata: " + err.message, 'error');
  }
}
function showNotification(message) {
  const notif = document.getElementById("notification");
  const notifText = document.getElementById("notificationText");
  notifText.innerText = message || "Yeni mesaj geldi!";
  notif.style.display = "block";

    // Sesli bildirim oynat
  const audio = document.getElementById("notifSound");
if (audio) {
  audio.currentTime = 0; // Baştan başlat
  audio.play().catch(err => {
    console.warn("Ses oynatılamadı:", err);
  });
}

  setTimeout(() => {
    notif.style.display = "none";
  }, 4000); // 4 saniye sonra gizle
}

function hideNotification() {
  document.getElementById("notification").style.display = "none";
}
async function resetPassword() {
  const email = document.getElementById("resetEmail").value;
  if (!email) {
    return showToast("Lütfen şifresini sıfırlamak istediğiniz e-posta adresini girin.", 'error');
  }

  try {
    const token = localStorage.getItem("token");
    const res = await fetch(`${backendUrl}/api/security/forgot-password`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      body: JSON.stringify({ email: email }),
    });

    if (!res.ok) {
        const errData = await res.json().catch(() => ({ message: "İstek başarısız." }));
        throw new Error(errData.message);
    }

    showToast("✅ Şifre sıfırlama linki e-posta adresinize gönderildi.", 'success');
    document.getElementById("resetEmail").value = "";
  } catch (err) {
    showToast(`❌ Hata: ${err.message}`, 'error');
  }
}

async function updateAvatar() {
  const token = localStorage.getItem("token");
  const fileInput = document.getElementById("updateAvatarInput");
  const file = fileInput.files[0];

  if (!file) {
    return showToast("Lütfen bir görsel seçin.", 'error');
  }

  const formData = new FormData();
  formData.append("file", file);

  try {
    const res = await fetch(`${backendUrl}/api/users/avatar`, {
      method: "POST",
      headers: {
        Authorization: "Bearer " + token,
      },
      body: formData,
    });

    if (!res.ok) throw new Error("Profil fotoğrafı güncellenemedi.");

    const data = await res.json();
    $("#profileAvatar").attr("src", data.avatarUrl); // Sunucudan dönen URL'i kullan
    showToast("✅ Profil fotoğrafı güncellendi.", 'success');
  } catch (err) {
    showToast("❌ Hata: " + err.message, 'error');
  }
}

// --- Arkadaşlık İstekleri ---
async function fetchAndRenderFriendRequests() {
  const token = localStorage.getItem("token");
  const list = document.getElementById("friendRequestsList");
  if (!list) return;
  list.innerHTML = '<li class="list-group-item text-muted">Yükleniyor...</li>';
  try {
    const res = await fetch(`${backendUrl}/api/user/friends/requests`, {
      headers: { Authorization: "Bearer " + token }
    });
    if (!res.ok) throw new Error("İstekler alınamadı");
    const requests = await res.json();
    if (requests.length === 0) {
      list.innerHTML = '<li class="list-group-item text-muted">İstek yok</li>';
      return;
    }
    list.innerHTML = '';
    requests.forEach(r => {
      // Gönderenin email ve kullanıcı adı birlikte gösterilsin
      const senderInfo = `${r.SenderName || ''} <span class='text-muted'>(${r.SenderEmail || ''})</span>`;
      const li = document.createElement('li');
      li.className = 'list-group-item d-flex align-items-center';
      li.innerHTML = `
        <div>
          <strong>${senderInfo}</strong>
          <br><small class="text-muted">${new Date(r.CreatedAt).toLocaleString('tr-TR')}</small>
        </div>
        <button class="btn btn-success btn-sm ms-auto me-1" onclick="acceptFriendRequest('${r.id}')">Kabul</button>
        <button class="btn btn-danger btn-sm" onclick="rejectFriendRequest('${r.id}')">Reddet</button>
      `;
      list.appendChild(li);
    });
  } catch (err) {
    list.innerHTML = `<li class="list-group-item text-danger">Hata: ${err.message}</li>`;
  }
}

async function acceptFriendRequest(id) {
  const token = localStorage.getItem("token");
  // Butonları disable et
  const btnAccept = document.querySelector(`button[onclick="acceptFriendRequest('${id}')"]`);
  const btnReject = document.querySelector(`button[onclick="rejectFriendRequest('${id}')"]`);
  if (btnAccept) btnAccept.disabled = true;
  if (btnReject) btnReject.disabled = true;
  try {
    const res = await fetch(`${backendUrl}/api/user/friends/requests/${id}/accept`, {
      method: "POST",
      headers: { Authorization: "Bearer " + token }
    });
    if (!res.ok) throw new Error("Kabul edilemedi");
    showToast("Arkadaşlık isteği kabul edildi", 'success');
  } catch (err) {
    showToast("Hata: " + err.message, 'error');
  } finally {
    // Her durumda listeyi güncelle
    await fetchAndRenderFriendRequests();
    await fetchAndStoreFriends();
  }
}

async function rejectFriendRequest(id) {
  const token = localStorage.getItem("token");
  // Butonları disable et
  const btnAccept = document.querySelector(`button[onclick="acceptFriendRequest('${id}')"]`);
  const btnReject = document.querySelector(`button[onclick="rejectFriendRequest('${id}')"]`);
  if (btnAccept) btnAccept.disabled = true;
  if (btnReject) btnReject.disabled = true;
  try {
    const res = await fetch(`${backendUrl}/api/user/friends/requests/${id}/reject`, {
      method: "POST",
      headers: {
        Authorization: "Bearer " + token,
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ reason: "Reddedildi" })
    });
    if (!res.ok) throw new Error("Reddedilemedi");
    showToast("Arkadaşlık isteği reddedildi", 'success');
  } catch (err) {
    showToast("Hata: " + err.message, 'error');
  } finally {
    // Her durumda listeyi güncelle
    await fetchAndRenderFriendRequests();
    await fetchAndStoreFriends();
  }
}

// Sayfa yüklendiğinde istekleri getir
if (window.location.pathname.includes('chat.html')) {
  document.addEventListener('DOMContentLoaded', () => {
    fetchAndRenderFriendRequests();
    fetchAndStoreFriends();
    // Her 10 saniyede bir otomatik güncelle
    setInterval(() => {
      fetchAndRenderFriendRequests();
      fetchAndStoreFriends();
    }, 10000);
  });
}



