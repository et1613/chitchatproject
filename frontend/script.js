let selectedReceiver = null;
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

function renderUserList(filter = "") {
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
  const groups = JSON.parse(localStorage.getItem("chitchat_groups")) || [];
  const filtered = friends.filter(u =>
    u.name.toLowerCase().includes(filter.toLowerCase())
  );
  const blocked = JSON.parse(localStorage.getItem("chitchat_blocked")) || [];
const visible = filtered.filter(u => !blocked.includes(u.email));
let html = ""; // <-- yukarı taşı
if (visible.length === 0) {
  html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
} else {
  visible.forEach(u => {
    // ...
  });
}
  // ✅ Grupları önce listele
groups.forEach(g => {
  html += `
    <li class="list-group-item d-flex align-items-center user-item group-item" data-name="${g.name}" data-email="group:${g.name}">
      <strong>👥 ${g.name}</strong>
    </li>
  `;
});
  if (filtered.length === 0) {
    html = `<li class="list-group-item text-muted">Kullanıcı bulunamadı</li>`;
  } else {
    filtered.forEach(u => {
  const avatar = u.avatar || (u.gender === "male" ? "img/avatar_male.png" : "img/avatar_female.png");
  html += `
    <li class="list-group-item d-flex align-items-center user-item" data-name="${u.name}" data-email="${u.email}">
      <img src="${avatar}" class="rounded-circle me-2" style="width:30px; height:30px;">
      ${u.name}
      <button class="btn btn-sm btn-outline-danger ms-auto" onclick="blockUser('${u.email}')">Engelle</button>
    </li>
  `;
});
  }

  document.getElementById("userList").innerHTML = html;

  document.querySelectorAll(".user-item").forEach(item => {
    item.addEventListener("click", () => {
      document.querySelectorAll(".user-item").forEach(i => i.classList.remove("active-user"));
      item.classList.add("active-user");

      selectedReceiver = {
  name: item.dataset.name,
  email: item.dataset.email
};


if (item.classList.contains("group-item")) {
  document.querySelector("#chatSection .card-header").innerText = `👥 Grup: ${selectedReceiver.name}`;
} else {
  document.querySelector("#chatSection .card-header").innerText = `${selectedReceiver.name} ile Sohbet`;
}
      loadMessages();
    });
  });
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
function populateFriendCheckboxes() {
  const friends = JSON.parse(localStorage.getItem("chitchat_friends")) || [];
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

// ✅ DOĞRU: Fonksiyon DOMContentLoaded'ın dışına alınmalı
async function createGroup() {
  const token = localStorage.getItem("token");
  if (!token) return showToast("Giriş yapmalısınız!", 'error');
  const groupName = document.getElementById("groupName").value.trim();
  const checkboxes = document.querySelectorAll("#friendCheckboxes input[type=checkbox]:checked");
  const memberEmails = Array.from(checkboxes).map(cb => cb.value);
  if (!groupName || memberEmails.length === 0) return showToast("Grup adı ve en az bir üye seçmelisiniz.", 'error');
  try {
    const res = await fetch(`${backendUrl}/api/chat/rooms`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify({ name: groupName, members: memberEmails })
    });
    if (!res.ok) throw new Error("Grup oluşturulamadı");
    showToast("✅ Grup oluşturuldu!", 'success');
    // Gerekirse grupları yeniden yükle
    // renderUserList();
    document.getElementById("groupName").value = "";
    $("#groupModal").modal("hide");
  } catch (err) {
    showToast("❌ Hata: " + err.message, 'error');
  }
}

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

  let chatRoomId = null;
  let isGroup = selectedReceiver.email.startsWith("group:");

  if (isGroup) {
    chatRoomId = selectedReceiver.name;
  } else {
    // Birebir sohbet için oda oluştur veya bul
    const userData = JSON.parse(localStorage.getItem("chitchat_user") || '{}');
    const emails = [userData.email, selectedReceiver.email].sort();
    const roomName = emails.join("_");
    // Oda oluşturma isteği
    try {
      const res = await fetch(`${backendUrl}/api/chat/rooms`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: "Bearer " + token
        },
        body: JSON.stringify({ name: roomName, members: emails })
      });
      if (!res.ok) throw new Error("Chat odası oluşturulamadı");
      const room = await res.json();
      chatRoomId = room.id || room.Id || room._id || roomName; // Fallback olarak oda adını kullan
    } catch (err) {
      showToast("❌ Oda oluşturulamadı: " + err.message, 'error');
      return;
    }
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
  if (isSignalRConnected && signalRConnection) {
    try {
      await signalRConnection.invoke("SendMessage", chatRoomId, content);
      console.log("SignalR message sent successfully");
    } catch (signalRError) {
      console.error("SignalR send error:", signalRError);
    }
  }

  // 4. Input'ları temizle
  messageInput.value = "";
  fileInput.value = "";
}

// ✅ SignalR Entegreli loadMessages() fonksiyonu
async function loadMessages() {
  const token = localStorage.getItem("token");
  if (!token) return;
  if (!selectedReceiver) return;

  let endpoint = "";
  let chatRoomId = null;

  if (selectedReceiver.email.startsWith("group:")) {
    // Grup mesajları
    chatRoomId = selectedReceiver.name;
    endpoint = `${backendUrl}/api/message/chat/${chatRoomId}`;
  } else {
    // Direkt mesajlar
    chatRoomId = selectedReceiver.email;
    endpoint = `${backendUrl}/api/message/chat/${selectedReceiver.email}`;
  }

  try {
    // 1. ✅ REST API ile geçmiş mesajları yükle (mevcut kod)
    const res = await fetch(endpoint, {
      headers: { Authorization: "Bearer " + token }
    });
    
    if (!res.ok) throw new Error("Mesajlar alınamadı");
    const messages = await res.json();
    renderMessages(messages);

    // 2. ✅ SignalR ile chat room'a katıl (YENİ EKLENEN)
    if (isSignalRConnected && signalRConnection) {
      try {
        // Önceki room'dan ayrıl
        if (window.currentChatRoom && window.currentChatRoom !== chatRoomId) {
          await signalRConnection.invoke("LeaveChatRoom", window.currentChatRoom);
          console.log(`Left previous room: ${window.currentChatRoom}`);
        }

        // Yeni room'a katıl
        await signalRConnection.invoke("JoinChatRoom", chatRoomId);
        window.currentChatRoom = chatRoomId; // Mevcut room'u kaydet
        console.log(`Joined room: ${chatRoomId}`);

        // Bağlantı durumunu güncelle
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
function renderMessages(messages) {
  const area = document.getElementById("messageArea");
  area.innerHTML = "";
  
  if (!messages || messages.length === 0) {
    area.innerHTML = `<div class='text-muted'>Hiç mesaj yok.</div>`;
    return;
  }
  
  messages.forEach(msg => {
    addMessageToChat(
      msg.senderName || msg.senderId || msg.sender, 
      msg.content || msg.message, 
      msg.timestamp || msg.createdAt || new Date()
    );
  });
  
  area.scrollTop = area.scrollHeight;
}

// ✅ YENİ EKLENEN: Mesajı chat'e eklemek için helper fonksiyon
function addMessageToChat(sender, content, timestamp) {
  const messageArea = document.getElementById("messageArea");
  
  // Timestamp formatla
  const time = new Date(timestamp).toLocaleTimeString('tr-TR', {
    hour: '2-digit', 
    minute: '2-digit'
  });
  
  // Mevcut kullanıcının kendi mesajını farklı stillendir
  const currentUser = JSON.parse(localStorage.getItem("chitchat_user") || "{}");
  const isOwnMessage = sender === currentUser.email || sender === currentUser.name;
  
  const messageDiv = document.createElement('div');
  messageDiv.className = `mb-2 p-2 rounded ${isOwnMessage ? 'bg-primary text-white ms-5' : 'bg-light me-5'}`;
  messageDiv.innerHTML = `
    ${!isOwnMessage ? `<strong>${sender}:</strong> ` : ''}
    ${content}
    <small class="d-block mt-1 ${isOwnMessage ? 'text-light' : 'text-muted'}">${time}</small>
  `;
  
  messageArea.appendChild(messageDiv);
  messageArea.scrollTop = messageArea.scrollHeight;
}

// ✅ YENİ EKLENEN: chat.html'deki addRealTimeMessage fonksiyonunu güncelle
// Bu fonksiyon chat.html'de SignalR event'inde çağrılacak
function addRealTimeMessage(sender, content, timestamp) {
  addMessageToChat(sender, content, timestamp);
  
  // Eğer şu anda farklı bir room'daysa mesajı gösterme
  const currentUser = JSON.parse(localStorage.getItem("chitchat_user") || "{}");
  if (sender !== currentUser.email && sender !== currentUser.name) {
    showNotification(`${sender}: ${content}`);
  }
}
function renderMessages(messages) {
  const area = document.getElementById("messageArea");
  area.innerHTML = "";
  if (!messages || messages.length === 0) {
    area.innerHTML = `<div class='text-muted'>Hiç mesaj yok.</div>`;
    return;
  }
  messages.forEach(msg => {
    area.innerHTML += `<div><strong>${msg.senderName || msg.senderId}:</strong> ${msg.content}</div>`;
  });
  area.scrollTop = area.scrollHeight;
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
function logout() {
  localStorage.removeItem("token");
  window.location.href = "index.html";
}
async function addFriend() {
  const token = localStorage.getItem("token");
  if (!token) return showToast("Giriş yapmalısınız!", 'error');
  const email = document.getElementById("friendEmail").value.trim();
  if (!email) return showToast("E-posta girin", 'error');
  try {
    const res = await fetch(`${backendUrl}/api/user/friends/add`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer " + token
      },
      body: JSON.stringify({ FriendEmail: email })
    });
    if (!res.ok) throw new Error("Arkadaş eklenemedi");
    showToast("✅ Arkadaş eklendi!", 'success');
    document.getElementById("friendEmail").value = "";
    await fetchAndStoreFriends();
  } catch (err) {
    showToast("❌ Hata: " + err.message, 'error');
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

async function fetchAndStoreFriends() {
  const token = localStorage.getItem("token");
  if (!token) return;
  try {
    const res = await fetch(`${backendUrl}/api/user/friends`, {
      headers: { Authorization: "Bearer " + token }
    });
    if (!res.ok) throw new Error("Arkadaşlar alınamadı");
    const friends = await res.json();
    // Backend'den gelen isimleri frontend ile uyumlu hale getir
    const mapped = friends.map(f => ({
      name: f.displayName || f.userName || f.email,
      email: f.email,
      avatar: f.profilePicture || "",
      gender: f.gender || "unknown"
    }));
    localStorage.setItem("chitchat_friends", JSON.stringify(mapped));
    renderUserList();
  } catch (err) {
    // Hata durumunda localStorage'daki eski listeyi kullanmaya devam et
  }
}



